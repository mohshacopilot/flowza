namespace Flowza.Messaging;

/// <summary>The broker an installation uses, from <c>Messaging:Transport</c> (ADR-0005). The same value for every service.</summary>
public enum MessageTransport
{
    /// <summary>Default: queues are tables in <c>flowza_messaging</c>; nothing extra to install.</summary>
    SqlServer,

    /// <summary>On-premises with RabbitMQ.</summary>
    RabbitMq,

    /// <summary>Azure. Coded now, tested once Azure access exists.</summary>
    AzureServiceBus,
}
