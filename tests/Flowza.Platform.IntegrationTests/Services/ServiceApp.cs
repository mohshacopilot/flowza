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
}

public sealed class ServiceApp<TMarker, TContext>(string name, string connectionStringName, string testDatabase)
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Program reads its connection string while the host is being built, so it must arrive as an
        // environment variable (WebApplicationFactory settings are applied too late for that).
        Environment.SetEnvironmentVariable($"ConnectionStrings__{connectionStringName}", TestSqlServer.ConnectionString(testDatabase));
        Environment.SetEnvironmentVariable("ConnectionStrings__messaging-db", TestSqlServer.ConnectionString("flowza_test_messaging"));
        Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", TestRabbitMq.ConnectionString());
        builder.UseEnvironment("Development");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

public static class ServiceApps
{
    public static TheoryData<string> Names => ["identity", "design", "runtime", "worker"];

    public static IServiceApp Create(string name) => name switch
    {
        "identity" => new ServiceApp<Flowza.Identity.Api.ApiAssembly, Flowza.Identity.Infrastructure.Persistence.IdentityServiceDbContext>(
            name, "identity-db", "flowza_test_identity"),
        "design" => new ServiceApp<Flowza.Design.Api.ApiAssembly, Flowza.Design.Infrastructure.Persistence.DesignDbContext>(
            name, "design-db", "flowza_test_design"),
        "runtime" => new ServiceApp<Flowza.Runtime.Api.ApiAssembly, Flowza.Runtime.Infrastructure.Persistence.RuntimeDbContext>(
            name, "runtime-db", "flowza_test_runtime"),
        "worker" => new ServiceApp<Flowza.Worker.ApiAssembly, Flowza.Worker.Persistence.WorkerDbContext>(
            name, "worker-db", "flowza_test_worker"),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown service."),
    };
}
