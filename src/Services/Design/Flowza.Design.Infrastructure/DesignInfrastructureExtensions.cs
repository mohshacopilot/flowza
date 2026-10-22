using Flowza.Design.Infrastructure.Persistence;
using Flowza.Messaging;
using Flowza.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowza.Design.Infrastructure;

public static class DesignInfrastructureExtensions
{
    /// <summary>Name of the connection string (set by the AppHost, PLT-03).</summary>
    public const string ConnectionStringName = "design-db";

    /// <summary>Registers the Design database, its readiness check and messaging with the outbox in this database (PLT-03, PLT-04, PLT-05).</summary>
    public static TBuilder AddDesignInfrastructure<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing. Start the services through the AppHost.");

        builder.Services.AddFlowzaDbContext<DesignDbContext>(connectionString);
        builder.Services.AddHealthChecks().AddSqlServer(connectionString, name: "database");
        if (builder.Environment.IsDevelopment())
        {
            // Development: the service migrates its own tables, as its first start-up step (PLT-03-AC1, T034).
            builder.Services.AddMigrationAtStartup<DesignDbContext>();
        }
        builder.AddFlowzaMessaging("design", connectionString, MessageRoutes.Default, outboxDbContext: typeof(DesignDbContext));
        return builder;
    }
}