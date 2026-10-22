namespace Flowza.Messaging;

/// <summary>
/// The transport an installation uses, from <c>Messaging:Transport</c> (ADR-0005). The same value for every service:
/// on-premises <see cref="SqlServer"/>, cloud <see cref="AzureServiceBus"/> (RabbitMQ removed, ADR-0005 amendment 3).
/// </summary>
public enum MessageTransport
{
    /// <summary>On-premises and default: queues are tables in <c>flowza_messaging</c>; nothing extra to install.</summary>
    SqlServer,

    /// <summary>Azure. Coded now, tested once Azure access exists.</summary>
    AzureServiceBus,
}
