using Flowza.Common.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowza.Persistence;

public static class MigrationExtensions
{
    /// <summary>
    /// Applies pending migrations. Used automatically in development only; production uses reviewed,
    /// idempotent SQL scripts run by the DBA (PLT-03, tech discussion PL4).
    /// </summary>
    public static async Task MigrateDatabaseAsync<TContext>(this IServiceProvider services, CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// Development only: applies the migrations as the first start-up step, so a service that starts while its
    /// database is down migrates as soon as it answers (T034). Production uses the DBA's scripts (T033).
    /// </summary>
    public static IServiceCollection AddMigrationAtStartup<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton<IStartupStep>(provider => new MigrationStartupStep<TContext>(provider));
    }

    private sealed class MigrationStartupStep<TContext>(IServiceProvider services) : IStartupStep
        where TContext : DbContext
    {
        public int Order => StartupStepOrder.Schema;

        public string Name => "database migration";

        public Task RunAsync(CancellationToken cancellationToken) => services.MigrateDatabaseAsync<TContext>(cancellationToken);
    }
}
