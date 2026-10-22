using Flowza.Worker.Persistence;
using Flowza.Messaging;
using Flowza.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowza.Worker;

public static class WorkerInfrastructureExtensions
{
    /// <summary>Name of the connection string (set by the AppHost, PLT-03).</summary>
    public const string ConnectionStringName = "worker-db";

    /// <summary>Registers the Worker database, its readiness check and messaging with the outbox in this database (PLT-03, PLT-04, PLT-05).</summary>
    public static TBuilder AddWorkerInfrastructure<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing. Start the services through the AppHost.");

        builder.Services.AddFlowzaDbContext<WorkerDbContext>(connectionString);
        builder.Services.AddHealthChecks().AddSqlServer(connectionString, name: "database");
        builder.AddFlowzaMessaging("worker", connectionString, MessageRoutes.Default, outboxDbContext: typeof(WorkerDbContext));
        return builder;
    }
}