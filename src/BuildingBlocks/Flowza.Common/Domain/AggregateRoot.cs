namespace Flowza.Common.Domain;

/// <summary>Something that happened inside an aggregate; stays inside the service (tech discussion T2, S6).</summary>
public interface IDomainEvent;

/// <summary>
/// Base for DDD aggregates (T2, S5): collects the domain events of one change, so the application layer can turn
/// them into integration events in the same transaction (S6).
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
}
