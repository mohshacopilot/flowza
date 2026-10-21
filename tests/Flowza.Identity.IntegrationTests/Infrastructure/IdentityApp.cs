using Flowza.Identity.Api;
using Flowza.Identity.Infrastructure.Persistence;
using Flowza.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Respawn;

namespace Flowza.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// The Identity service started in memory against <c>flowza_test_identity</c> (tech discussion T8, TE3). One instance
/// per test collection; <see cref="ResetDataAsync"/> clears the business data between tests (Respawn), keeping the
/// seeded roles, the OpenIddict clients and the Data Protection keys.
/// </summary>
public sealed class IdentityApp : WebApplicationFactory<ApiAssembly>, IAsyncLifetime
{
    public const string Database = "flowza_test_identity";

    private TestDatabaseLock? _lock;
    private Respawner? _respawner;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _lock = await TestDatabaseLock.AcquireAsync(Database, ct);

        // A first start on an empty database, as on a new installation (PLT-03); the service migrates in Development.
        await TestDatabase.DropServiceTablesAsync(Database, ct);
        _ = Services;

        await using var connection = new SqlConnection(TestSqlServer.ConnectionString(Database));
        await connection.OpenAsync(ct);
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            SchemasToInclude = ["dbo"],
            TablesToIgnore = ["__EFMigrationsHistory", "Roles", "OpenIddictApplications", "OpenIddictScopes", "DataProtectionKeys"],
        });
    }

    public async Task ResetDataAsync()
    {
        await using var connection = new SqlConnection(TestSqlServer.ConnectionString(Database));
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
    }

    public async Task<T> WithDbContextAsync<T>(Func<IdentityServiceDbContext, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Program reads its connection strings while the host is being built, so they arrive as environment variables.
        Environment.SetEnvironmentVariable("ConnectionStrings__identity-db", TestSqlServer.ConnectionString(Database));
        Environment.SetEnvironmentVariable("ConnectionStrings__messaging-db", TestSqlServer.ConnectionString("flowza_test_messaging"));
        builder.UseEnvironment("Development");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_lock is not null)
        {
            await _lock.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>All Identity integration tests share one service instance and run one after another.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IdentityTestGroup : ICollectionFixture<IdentityApp>
{
    public const string Name = "Identity";
}
