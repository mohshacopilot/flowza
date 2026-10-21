namespace Flowza.Contracts;

/// <summary>
/// Base for every event that leaves a service (tech discussion T4, MS1–MS2). The standard fields prepare for the
/// Audit service (ADR-0001). Events carry IDs only, never names, emails, passwords or form values (constitution 11.2).
/// A released event never changes; a changed shape gets a new version namespace (<c>…Events.V2</c>; constitution 3.5).
/// </summary>
public abstract record IntegrationEvent
{
    /// <summary>Unique per event; receivers use it to ignore duplicates (constitution 3.4).</summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>When the change happened (UTC).</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>The user who caused it; <c>null</c> for the system.</summary>
    public Guid? ActorId { get; init; }

    /// <summary>The trace reference that follows one user action across services (PLT-05).</summary>
    public string? CorrelationId { get; init; }
}
