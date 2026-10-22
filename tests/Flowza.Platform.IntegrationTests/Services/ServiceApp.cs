using Flowza.Messaging;
using Flowza.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowza.Platform.IntegrationTests.Services;

/// <summary>One service started in memory against its <c>flowza_test_*</c> database (tech discussion T8, TE3).</summary>
public interface IServiceApp : IAsyncDisposable
{
    string Name { get; }

    HttpClient CreateClient();

    Task<IReadOnlyList<string>> AppliedMigrationsAsync(CancellationToken cancellationToken);

    /// <summary>All migrations the service's code contains.</summary>
    IReadOnlyList<string> AllMigrations();

    /// <summary>The production database scripts the service writes with <c>--database-scripts</c> (T033).</summary>
    Task<IReadOnlyList<DatabaseScript>> ProductionScriptsAsync();
}

public sealed class ServiceApp<TMarker, TContext>(string name, string connectionStringName, string testDatabase, string environment = "Development")
    : WebApplicationFactory<TMarker>, IServiceApp
    where TMarker : class
    where TContext : DbContext
{
    public string Name { get; } = name;

    public async Task<IReadOnlyList<string>> AppliedMigrationsAsync(CancellationToken cancellationToken)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        return [.. await context.Database.GetAppliedMigrationsAsync(cancellationToken)];
    }

    public IReadOnlyList<string> AllMigrations()
    {
        using var scope = Services.CreateScope();
        return [.. scope.ServiceProvider.GetRequiredService<TContext>().Database.GetMigrations()];
    }

    public Task<IReadOnlyList<DatabaseScript>> ProductionScriptsAsync() => ProductionDatabaseScripts.CreateAsync<TContext>(Services, Name);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Program reads its connection string while the host is being built, so it must arrive as an
        // environment variable (WebApplicationFactory settings are applied too late for that).
        Environment.SetEnvironmentVariable($"ConnectionStrings__{connectionStringName}", TestSqlServer.ConnectionString(testDatabase));
        Environment.SetEnvironmentVariable("ConnectionStrings__messaging-db", TestSqlServer.ConnectionString("flowza_test_messaging"));
        builder.UseEnvironment(environment);
        if (environment != "Development")
        {
            // What a real installation sets; log files go to a temporary folder, not into the project.
            builder.UseSetting("Flowza:Identity:Authority", "https://identity.test/");
            builder.UseSetting("Flowza:Logging:Folder", Path.Combine(Path.GetTempPath(), "flowza-service-test-logs"));
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Tests that start the services or reset their tables share the <c>flowza_test_*</c> service tables, so they run
/// one after another.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ServiceDatabasesTestGroup : ICollectionFixture<IdentityDatabaseLockFixture>
{
    public const string Name = "ServiceDatabases";
}

/// <summary>
/// The Identity integration tests (another test process) also reset <c>flowza_test_identity</c>; this lock keeps the
/// two from running against it at the same time.
/// </summary>
public sealed class IdentityDatabaseLockFixture : IAsyncLifetime
{
    private TestDatabaseLock? _lock;

    public async ValueTask InitializeAsync() =>
        _lock = await TestDatabaseLock.AcquireAsync("flowza_test_identity", TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_lock is not null)
        {
            await _lock.DisposeAsync();
        }
    }
}

public static class ServiceApps
{
    public static TheoryData<string> Names => ["identity", "design", "runtime", "worker"];

    public static string DatabaseOf(string name) => $"flowza_test_{name}";

    public static IServiceApp Create(string name, string environment = "Development") => name switch
    {
        "identity" => new ServiceApp<Flowza.Identity.Api.ApiAssembly, Flowza.Identity.Infrastructure.Persistence.IdentityServiceDbContext>(
            name, "identity-db", "flowza_test_identity", environment),
        "design" => new ServiceApp<Flowza.Design.Api.ApiAssembly, Flowza.Design.Infrastructure.Persistence.DesignDbContext>(
            name, "design-db", "flowza_test_design", environment),
        "runtime" => new ServiceApp<Flowza.Runtime.Api.ApiAssembly, Flowza.Runtime.Infrastructure.Persistence.RuntimeDbContext>(
            name, "runtime-db", "flowza_test_runtime", environment),
        "worker" => new ServiceApp<Flowza.Worker.ApiAssembly, Flowza.Worker.Persistence.WorkerDbContext>(
            name, "worker-db", "flowza_test_worker", environment),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown service."),
    };
}
