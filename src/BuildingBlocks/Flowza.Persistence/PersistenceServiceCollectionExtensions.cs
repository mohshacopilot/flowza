using Flowza.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flowza.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>SQL Server compatibility level used by every Flowza database: SQL Server 2019 (constitution 6.3).</summary>
    public const int SqlServerCompatibilityLevel = 150;

    /// <summary>
    /// Registers a service's DbContext on SQL Server with compatibility level 150 and the audit/soft-delete
    /// interceptor. A host registers its own <see cref="ICurrentUser"/> (e.g. from the HTTP request); otherwise the
    /// system user is used.
    /// </summary>
    public static IServiceCollection AddFlowzaDbContext<TContext>(this IServiceCollection services, string connectionString)
        where TContext : FlowzaDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ICurrentUser>(_ => SystemUser.Instance);
        services.TryAddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<TContext>((provider, options) => options
            .UseSqlServer(connectionString, sql => sql.UseCompatibilityLevel(SqlServerCompatibilityLevel))
            .AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>()));

        return services;
    }
}
