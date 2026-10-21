using System.Collections.Concurrent;

namespace Flowza.ServiceDefaults.Security;

/// <summary>
/// Users whose access tokens must be refused at once, before the tokens expire (K7; plan 001 §5.4, PA4). Filled from
/// the <c>UserDeactivated</c> / <c>UserActivated</c> events and at start-up.
/// </summary>
public interface IUserDenyList
{
    bool IsDenied(Guid userId);

    /// <summary>The user was deactivated at <paramref name="changedAt"/>.</summary>
    void Deny(Guid userId, DateTimeOffset changedAt);

    /// <summary>The user was activated again at <paramref name="changedAt"/>.</summary>
    void Allow(Guid userId, DateTimeOffset changedAt);
}

/// <summary>
/// In-memory deny list: a concurrent dictionary, so the check on every request is O(1) and lock-free. Each entry keeps
/// the time of the change it comes from; an older event never overrides a newer one, so events that arrive out of
/// order (they travel separately) still leave the right state.
/// </summary>
public sealed class UserDenyList : IUserDenyList
{
    private readonly ConcurrentDictionary<Guid, (bool Denied, DateTimeOffset ChangedAt)> _entries = new();

    public int DeniedCount => _entries.Count(e => e.Value.Denied);

    public bool IsDenied(Guid userId) => _entries.TryGetValue(userId, out var entry) && entry.Denied;

    public void Deny(Guid userId, DateTimeOffset changedAt) => Set(userId, denied: true, changedAt);

    public void Allow(Guid userId, DateTimeOffset changedAt) => Set(userId, denied: false, changedAt);

    private void Set(Guid userId, bool denied, DateTimeOffset changedAt) =>
        _entries.AddOrUpdate(
            userId,
            (denied, changedAt),
            (_, current) => changedAt >= current.ChangedAt ? (denied, changedAt) : current);
}

/// <summary>Where a service finds the users that are inactive right now, at start-up (the Identity service: its database).</summary>
public interface IUserDenyListSource
{
    Task<IReadOnlyCollection<Guid>> InactiveUserIdsAsync(CancellationToken cancellationToken);
}
