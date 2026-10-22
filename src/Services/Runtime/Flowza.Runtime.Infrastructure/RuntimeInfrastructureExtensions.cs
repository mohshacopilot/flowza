using Flowza.Runtime.Infrastructure.Persistence;
using Flowza.Messaging;
using Flowza.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowza.Runtime.Infrastructure;

public static class RuntimeInfrastructureExtensions
{
    /// <summary>Name of the connection string (set by the AppHost, PLT-03).</summary>
    public const string ConnectionStringName = "runtime-db";

    /// <summary>Registers the Runtime database, its readiness check and messaging with the outbox in this database (PLT-03, PLT-04, PLT-05).</summary>
    public static TBuilder AddRuntimeInfrastructure<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing. Start the services through the AppHost.");

        builder.Services.AddFlowzaDbContext<RuntimeDbContext>(connectionString);
        builder.Services.AddHealthChecks().AddSqlServer(connectionString, name: "database");
        builder.AddFlowzaMessaging("runtime", connectionString, MessageRoutes.Default, outboxDbContext: typeof(RuntimeDbContext));
        return builder;
    }
}