using Flowza.Identity.Infrastructure.Persistence;
using Flowza.Messaging;
using Flowza.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Flowza.Identity.Infrastructure;

public static class IdentityInfrastructureExtensions
{
    /// <summary>Name of the connection string (set by the AppHost, PLT-03).</summary>
    public const string ConnectionStringName = "identity-db";

    /// <summary>Data Protection application name: keys made by one instance are readable by the others.</summary>
    public const string DataProtectionApplicationName = "Flowza.Identity";

    /// <summary>Registers the Identity database, its readiness check and messaging with the outbox in this database (PLT-03, PLT-04, PLT-05).</summary>
    public static TBuilder AddIdentityInfrastructure<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing. Start the services through the AppHost.");

        builder.Services.AddFlowzaDbContext<IdentityServiceDbContext>(connectionString);
        builder.Services.AddHealthChecks().AddSqlServer(connectionString, name: "database");
        builder.AddFlowzaMessaging("identity", connectionString, MessageRoutes.Default);

        builder.Services.AddDataProtection().SetApplicationName(DataProtectionApplicationName);
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(provider =>
            new ConfigureOptions<KeyManagementOptions>(options =>
                options.XmlRepository = new DatabaseXmlRepository(provider.GetRequiredService<IServiceScopeFactory>())));

        return builder;
    }
}
