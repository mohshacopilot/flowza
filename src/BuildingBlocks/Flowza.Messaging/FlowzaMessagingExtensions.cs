using JasperFx;
using JasperFx.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Attributes;
using Wolverine.AzureServiceBus;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Persistence;
using Wolverine.Persistence.Durability;
using Wolverine.SqlServer;
using Flowza.Common.Hosting;
using Flowza.Contracts;

namespace Flowza.Messaging;

/// <summary>
/// Sets up Wolverine for a Flowza service (plan 001 section 8; ADR-0004, ADR-0005; tech discussion T4):
/// in-process mediator, transactional outbox and durable inbox in the service's <b>own</b> database, retry and
/// dead-letter policy, and one of two transports chosen by <c>Messaging:Transport</c>: SqlServer (on-premises,
/// default) or AzureServiceBus (cloud).
/// </summary>
public static class FlowzaMessagingExtensions
{
    /// <summary>Schema of Wolverine's outbox/inbox tables inside each service database.</summary>
    public const string EnvelopeSchema = "wolverine";

    /// <summary>Message header carrying an integration event's EventId, used for deduplication (MS3).</summary>
    public const string EventIdHeader = "flowza-event-id";

    public const string TransportKey = "Messaging:Transport";
    public const string SqlServerTransportConnectionName = "messaging-db";
    public const string AzureServiceBusConnectionName = "azure-servicebus";

    public static MessageTransport ReadTransport(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var value = configuration[TransportKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return MessageTransport.SqlServer;
        }

        // IsDefined: Enum.TryParse also accepts numbers such as "5", which would leave no transport configured.
        return Enum.TryParse<MessageTransport>(value, ignoreCase: true, out var transport) && Enum.IsDefined(transport)
            ? transport
            : throw new InvalidOperationException(
                $"'{TransportKey}' is '{value}'. Use SqlServer (on-premises) or AzureServiceBus (cloud) (ADR-0005).");
    }

    /// <param name="serviceName">The service's name, also its queue name (e.g. "runtime").</param>
    /// <param name="serviceDatabaseConnectionString">The service's own database: holds the outbox and inbox.</param>
    /// <param name="routes">Which services receive which events.</param>
    /// <param name="configure">Extra Wolverine settings (e.g. handler discovery in tests).</param>
    /// <param name="envelopeSchema">Schema of the outbox/inbox tables; only tests change it.</param>
    /// <param name="outboxDbContext">The service's DbContext, whose saves carry the outbox (null: the service has none).</param>
    public static TBuilder AddFlowzaMessaging<TBuilder>(
        this TBuilder builder,
        string serviceName,
        string serviceDatabaseConnectionString,
        MessageRoutes routes,
        Action<WolverineOptions>? configure = null,
        string envelopeSchema = EnvelopeSchema,
        Type? outboxDbContext = null)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceDatabaseConnectionString);
        ArgumentNullException.ThrowIfNull(routes);

        var transport = ReadTransport(builder.Configuration);
        var queue = MessageRoutes.QueueName(serviceName);

        // PL4 (T033): only Development creates Wolverine's tables and queues by itself. Everywhere else the DBA creates
        // them with the reviewed scripts (ProductionDatabaseScripts), and the service only checks they are there.
        var createStorage = builder.Environment.IsDevelopment();

        // T034: Wolverine needs its database to start, so it starts as a start-up step once the database answers
        // (StartupStepRunner), not with the host; a service then starts while the database is down.
        DeferredHostedServices.Defer(builder.Services, "messaging", StartupStepOrder.Messaging, () => builder.Services.AddWolverine(options =>
        {
            options.ServiceName = queue;

            // Outbox and inbox live in the service's own database, so a business change and its events are
            // saved in one transaction (constitution 3.3). Wolverine keeps SQL Server queue tables in the database of
            // its main store, so with that transport the shared messaging database is the main store (see
            // ConfigureSqlServer) and the service's database an ancillary store enrolled with the service's DbContext
            // (ADR-0005 amendment); the other transports use the service's database as the main store.
            if (transport == MessageTransport.SqlServer)
            {
                var serviceStore = options.PersistMessagesWithSqlServer(serviceDatabaseConnectionString, envelopeSchema, MessageStoreRole.Ancillary);
                if (outboxDbContext is not null)
                {
                    serviceStore.Enroll(outboxDbContext);
                }
            }
            else
            {
                options.PersistMessagesWithSqlServer(serviceDatabaseConnectionString, envelopeSchema, MessageStoreRole.Main);
            }
            options.AutoBuildMessageStorageOnStartup = createStorage ? AutoCreate.CreateOrUpdate : AutoCreate.None;
            options.UseEntityFrameworkCoreTransactions();
            options.Policies.AutoApplyTransactions();
            options.Policies.UseDurableOutboxOnAllSendingEndpoints();
            options.Policies.UseDurableInboxOnAllListeners();
            options.Policies.UseDurableLocalQueues();

            // MS3: every handler of an integration event runs once per EventId, even if the event is delivered or
            // published twice (constitution 3.4). Senders copy the EventId into a message header (headers travel
            // on every transport) and receivers deduplicate on it, so no handler can forget it.
            options.Durability.MessageDeduplicationMode = MessageDeduplicationMode.CompareByHash;
            options.Durability.DeduplicationWindow = 7.Days();
            options.Policies.AllSenders(sender => sender.CustomizeOutgoingMessagesOfType<IntegrationEvent>(
                (envelope, integrationEvent) => envelope.Headers[EventIdHeader] = integrationEvent.EventId.ToString("N")));
            options.Policies.RequireDeduplicationId(
                chain => typeof(IntegrationEvent).IsAssignableFrom(chain.MessageType),
                new DeduplicationRequirement { Source = ValueSource.Header, Key = EventIdHeader });

            // MS4: 3 quick retries, then 5 s, 30 s, 5 min, then the dead-letter queue.
            options.OnException<Exception>()
                .RetryTimes(3)
                .Then.ScheduleRetry(5.Seconds(), 30.Seconds(), 5.Minutes())
                .Then.MoveToErrorQueue();

            switch (transport)
            {
                case MessageTransport.SqlServer:
                    ConfigureSqlServer(options, builder.Configuration, queue, routes, createStorage);
                    break;
                case MessageTransport.AzureServiceBus:
                    ConfigureAzureServiceBus(options, builder.Configuration, queue, routes);
                    break;
            }

            configure?.Invoke(options);
        }));

        // Readiness reports the transport (so mixed settings across services are visible, ADR-0005) and
        // whether its broker can be reached (PLT-05).
        var health = builder.Services.AddHealthChecks().AddCheck(
            "messaging",
            () => HealthCheckResult.Healthy(data: new Dictionary<string, object> { ["transport"] = transport.ToString() }));
        switch (transport)
        {
            case MessageTransport.SqlServer:
                health.AddSqlServer(Required(builder.Configuration, SqlServerTransportConnectionName), name: "message-transport");
                break;
            case MessageTransport.AzureServiceBus:
                health.AddCheck("message-transport", new AzureServiceBusHealthCheck(Required(builder.Configuration, AzureServiceBusConnectionName)));
                break;
        }

        return builder;
    }

    private static string Required(IConfiguration configuration, string connectionName) =>
        configuration.GetConnectionString(connectionName) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Connection string '{connectionName}' is missing for the configured message transport (ADR-0005).");

    private static void ConfigureSqlServer(WolverineOptions options, IConfiguration configuration, string queue, MessageRoutes routes, bool createQueues)
    {
        // Queues are tables in the shared flowza_messaging database; it holds no business data (ADR-0004). It is the
        // main store, because Wolverine creates the queue tables in the main store's database: as an ancillary store
        // every service got its own copy of the queues in its own database and events never left it (T018 finding).
        var sqlServer = options.UseSqlServerPersistenceAndTransport(
            Required(configuration, SqlServerTransportConnectionName),
            $"wolverine_{queue}",
            "queues",
            MessageStoreRole.Main);
        if (createQueues)
        {
            sqlServer.AutoProvision();
        }

        options.ListenToSqlServerQueue(queue);
        foreach (var (type, subscribers) in routes.Subscribers)
        {
            foreach (var subscriber in subscribers)
            {
                options.PublishMessage(type).ToSqlServerQueue(subscriber);
            }
        }
    }

    private static void ConfigureAzureServiceBus(WolverineOptions options, IConfiguration configuration, string queue, MessageRoutes routes)
    {
        // Coded now, tested once Azure access exists (ADR-0005). One topic per event type, one subscription per service.
        options.UseAzureServiceBus(Required(configuration, AzureServiceBusConnectionName)).AutoProvision();

        foreach (var type in routes.Subscribers.Keys)
        {
            options.PublishMessage(type).ToAzureServiceBusTopic(MessageRoutes.TopicName(type));
        }

        foreach (var type in routes.TypesFor(queue))
        {
            options.ListenToAzureServiceBusSubscription(queue).FromTopic(MessageRoutes.TopicName(type));
        }
    }
}
