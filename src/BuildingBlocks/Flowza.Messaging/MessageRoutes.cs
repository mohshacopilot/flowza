using Flowza.Contracts.Identity.Events.V1;

namespace Flowza.Messaging;

/// <summary>
/// Which services receive which integration events. One explicit map gives the same behaviour on every
/// transport (ADR-0005): each subscribing service has its own queue, so every subscriber gets its own copy
/// (publish/subscribe), and a service never needs to know how the others are deployed.
/// </summary>
public sealed class MessageRoutes
{
    private readonly Dictionary<Type, HashSet<string>> _subscribers = [];

    /// <summary>The services that run the platform; each has its own queue.</summary>
    public static IReadOnlyList<string> AllServices { get; } = ["identity", "design", "runtime", "worker"];

    /// <summary>The platform's routes; features add theirs here as they introduce events.</summary>
    public static MessageRoutes Default => new MessageRoutes()
        // Every service keeps a deny list of deactivated users (K7; plan 001 §5.4, PA4).
        .Route<UserDeactivated>([.. AllServices])
        .Route<UserActivated>([.. AllServices]);

    public IReadOnlyDictionary<Type, IReadOnlyCollection<string>> Subscribers =>
        _subscribers.ToDictionary(r => r.Key, r => (IReadOnlyCollection<string>)r.Value);

    /// <summary>Sends <typeparamref name="TMessage"/> to each of the named services.</summary>
    public MessageRoutes Route<TMessage>(params string[] subscribingServices) => Route(typeof(TMessage), subscribingServices);

    public MessageRoutes Route(Type messageType, params string[] subscribingServices)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(subscribingServices);
        if (!_subscribers.TryGetValue(messageType, out var services))
        {
            services = new HashSet<string>(StringComparer.Ordinal);
            _subscribers[messageType] = services;
        }

        foreach (var service in subscribingServices)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(service);
            services.Add(QueueName(service));
        }

        return this;
    }

    /// <summary>Message types the given service receives.</summary>
    public IReadOnlyList<Type> TypesFor(string service)
    {
        var queue = QueueName(service);
        return [.. _subscribers.Where(r => r.Value.Contains(queue)).Select(r => r.Key)];
    }

    /// <summary>Queue, subscription or SQL queue name of a service (lower case, e.g. "runtime").</summary>
    public static string QueueName(string service)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        return service.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Exchange or topic name of a message type: its full .NET name in lower case, with any character that brokers
    /// do not accept (from nested or generic types, e.g. '+', '`', '[') replaced by '.'.
    /// </summary>
    public static string TopicName(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        var name = (messageType.FullName ?? messageType.Name).ToLowerInvariant();
        return new string([.. name.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '.')]);
    }
}
