using Flowza.Identity.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Flowza.Cli;

/// <summary>
/// A short-lived host with the Identity service's own application and infrastructure code, so the tool follows the
/// same rules (password policy, events through the outbox) as the service. It never listens for messages and does not
/// join the services' message-processing cluster.
/// </summary>
internal static class CliHost
{
    public static IHost Build(IReadOnlyDictionary<string, string?>? settings)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = "Flowza.Cli",
            ContentRootPath = AppContext.BaseDirectory,
            DisableDefaults = true,
        });

        builder.Configuration
            .AddUserSecrets("flowza-dev", reloadOnChange: false)
            .AddEnvironmentVariables();
        if (settings is not null)
        {
            builder.Configuration.AddInMemoryCollection(settings);
        }

        AddDevelopmentConnections(builder.Configuration);

        // Warnings only: the tool's own output is the console text; nothing personal is logged.
        builder.Logging.AddSimpleConsole().SetMinimumLevel(LogLevel.Warning);

        builder.AddIdentityInfrastructure();
        builder.Services.ConfigureWolverine(options =>
        {
            // Keep the outbox (events are saved with the change and sent), but no node, agents or listeners: a running
            // Identity service must keep receiving its own messages.
            options.Durability.Mode = DurabilityMode.ServerlessWithOutbox;
            foreach (var endpoint in options.Transports.SelectMany(t => t.Endpoints()).Where(e => e.IsListener))
            {
                endpoint.IsListener = false;
            }
        });

        return builder.Build();
    }

    /// <summary>
    /// On a developer PC the services get their connection strings from the AppHost; the tool builds the same ones from
    /// the flowza-dev user secrets (Sql:Server, Sql:User, Sql:Password) when none are given.
    /// </summary>
    private static void AddDevelopmentConnections(ConfigurationManager configuration)
    {
        if (string.IsNullOrEmpty(configuration["Sql:Password"]))
        {
            return;
        }

        string Database(string name) => new SqlConnectionStringBuilder
        {
            DataSource = configuration["Sql:Server"] ?? "localhost",
            InitialCatalog = name,
            UserID = configuration["Sql:User"] ?? "flowza_app",
            Password = configuration["Sql:Password"],
            Encrypt = true,
            TrustServerCertificate = true,
            ApplicationName = "Flowza CLI",
        }.ConnectionString;

        var missing = new Dictionary<string, string?>();
        if (string.IsNullOrEmpty(configuration.GetConnectionString(IdentityInfrastructureExtensions.ConnectionStringName)))
        {
            missing[$"ConnectionStrings:{IdentityInfrastructureExtensions.ConnectionStringName}"] = Database("flowza_identity");
        }

        if (string.IsNullOrEmpty(configuration.GetConnectionString("messaging-db")))
        {
            missing["ConnectionStrings:messaging-db"] = Database("flowza_messaging");
        }

        configuration.AddInMemoryCollection(missing);
    }
}
