using Flowza.Messaging;
using Flowza.Persistence;
using Flowza.ServiceDefaults;
using Flowza.Testing;
using JasperFx.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowza.Platform.IntegrationTests.Messaging;

/// <summary>
/// Two small test services that talk through the real transports: "sender" publishes, "receiver" subscribes.
/// Like real services they have <b>different</b> databases (sender <c>flowza_test_worker</c>, receiver
/// <c>flowza_test_runtime</c>, own schemas), so an event must cross from one service to another (T018 finding); the
/// SQL Server transport uses <c>flowza_test_messaging</c>.
/// </summary>
internal static class MessagingHosts
{
    public const string Sender = "msgtest_sender";
    public const string Receiver = "msgtest_receiver";
    public const string Database = "flowza_test_worker";
    public const string ReceiverDatabase = "flowza_test_runtime";

    private const string CreateBusinessTableSql = $"""
        IF SCHEMA_ID(N'{MessagingTestDbContext.Schema}') IS NULL EXEC(N'CREATE SCHEMA [{MessagingTestDbContext.Schema}]');
        IF OBJECT_ID(N'[{MessagingTestDbContext.Schema}].[Orders]') IS NULL
            CREATE TABLE [{MessagingTestDbContext.Schema}].[Orders] ([Id] uniqueidentifier NOT NULL PRIMARY KEY, [Name] nvarchar(100) NOT NULL);
        """;

    public static MessageRoutes Routes => new MessageRoutes().Route<TestOrderPlaced>(Receiver);

    /// <summary>Azure Service Bus tests run only when this environment variable holds a test namespace (ADR-0005).</summary>
    public static string? AzureServiceBusConnectionString =>
        Environment.GetEnvironmentVariable("FLOWZA_ConnectionStrings__azure-servicebus");

    public static async Task<IHost> StartSenderAsync(string transport, CancellationToken cancellationToken, bool reset = true)
    {
        var connectionString = TestSqlServer.ConnectionString(Database);
        var builder = CreateBuilder(transport);
        builder.Services.AddFlowzaDbContext<MessagingTestDbContext>(connectionString);
        builder.AddFlowzaMessaging(
            Sender,
            connectionString,
            Routes,
            options => options.Discovery.DisableConventionalDiscovery(),
            MessagingTestDbContext.EnvelopeSchema,
            typeof(MessagingTestDbContext));

        var host = builder.Build();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
            await context.Database.ExecuteSqlRawAsync(CreateBusinessTableSql, cancellationToken);
        }

        return await StartAsync(host, reset, cancellationToken);
    }

    public static async Task<IHost> StartReceiverAsync(string transport, CancellationToken cancellationToken, bool reset = true)
    {
        var builder = CreateBuilder(transport);
        builder.Services.AddSingleton<ReceivedLog>();
        builder.AddFlowzaMessaging(
            Receiver,
            TestSqlServer.ConnectionString(ReceiverDatabase),
            Routes,
            options =>
            {
                options.Discovery.DisableConventionalDiscovery();
                options.Discovery.IncludeType(typeof(TestOrderPlacedHandler));
            },
            "wolverine_msgtest_receiver");

        return await StartAsync(builder.Build(), reset, cancellationToken);
    }

    /// <summary>
    /// Starts a test host. With <paramref name="reset"/>, its queues and message tables are emptied first, so
    /// leftovers of earlier runs (for example on the other transport) cannot interfere.
    /// </summary>
    private static async Task<IHost> StartAsync(IHost host, bool reset, CancellationToken cancellationToken)
    {
        if (reset)
        {
            await host.ResetResourceState(cancellationToken);
        }

        await host.StartAsync(cancellationToken);
        return host;
    }

    public static async Task StopAsync(IHost? host)
    {
        if (host is null)
        {
            return;
        }

        await host.StopAsync();
        host.Dispose();
    }

    private static HostApplicationBuilder CreateBuilder(string transport)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Development" });

        // The same tracing set-up as the real services, so the trace test proves what production does.
        builder.AddFlowzaTelemetry();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [FlowzaMessagingExtensions.TransportKey] = transport,
            [$"ConnectionStrings:{FlowzaMessagingExtensions.SqlServerTransportConnectionName}"] = TestSqlServer.ConnectionString("flowza_test_messaging"),
            [$"ConnectionStrings:{FlowzaMessagingExtensions.AzureServiceBusConnectionName}"] = AzureServiceBusConnectionString,
        });
        return builder;
    }
}
