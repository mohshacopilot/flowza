namespace Flowza.Identity.Domain.Organization;

/// <summary>
/// The reporting managers above a user, nearest first (plan 002 §§5.1, 8): <see cref="Managers"/>[0] is the user's
/// manager, [1] that manager's manager, and so on. Used for the loop check (ORG-04-AC2) and rule c (N levels up).
/// Inactive managers stay in the chain: they still count as a level (C5). The walk keeps a visited set and a step limit,
/// so a damaged chain (a loop that slipped in) ends the walk instead of hanging it; such a chain is
/// <see cref="IsBroken"/>. Cost: O(chain length).
/// </summary>
public sealed class ManagerChain
{
    /// <summary>Longest chain walked; real reporting lines are far shorter.</summary>
    public const int MaxLength = 100;

    private ManagerChain(Guid userId, IReadOnlyList<Guid> managers, bool isBroken)
    {
        UserId = userId;
        Managers = managers;
        IsBroken = isBroken;
    }

    /// <summary>The user the chain starts from (not part of <see cref="Managers"/>).</summary>
    public Guid UserId { get; }

    public IReadOnlyList<Guid> Managers { get; }

    /// <summary>The walk met a user twice or reached <see cref="MaxLength"/>; the chain must not be trusted.</summary>
    public bool IsBroken { get; }

    /// <summary>Whether <paramref name="userId"/> is the start or any manager in the chain.</summary>
    public bool Contains(Guid userId) => userId == UserId || Managers.Contains(userId);

    /// <summary>The manager <paramref name="levels"/> steps up (1 = the user's own manager), or null when the chain is shorter.</summary>
    public Guid? ManagerAt(int levels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(levels);
        return levels <= Managers.Count ? Managers[levels - 1] : null;
    }

    /// <summary>Walks the chain with an in-memory lookup (tests, small sets).</summary>
    public static ManagerChain Walk(Guid userId, Func<Guid, Guid?> managerOf)
    {
        ArgumentNullException.ThrowIfNull(managerOf);
        var builder = new Builder(userId);
        while (builder.TryAdd(managerOf(builder.Current)))
        {
        }

        return builder.Build();
    }

    /// <summary>
    /// Builds a chain one step at a time, for walks that read each manager from the database:
    /// <c>while (builder.TryAdd(await ManagerOf(builder.Current))) { }</c>.
    /// </summary>
    public sealed class Builder(Guid userId)
    {
        private readonly List<Guid> _managers = [];
        private readonly HashSet<Guid> _seen = [userId];
        private bool _broken;
        private bool _ended;

        /// <summary>The user whose manager is read next.</summary>
        public Guid Current => _managers.Count == 0 ? userId : _managers[^1];

        /// <summary>
        /// Adds the manager of <see cref="Current"/>. Returns false when the walk ends: no manager (the top), a user
        /// met before (a loop) or the step limit.
        /// </summary>
        public bool TryAdd(Guid? managerId)
        {
            if (_ended)
            {
                return false;
            }

            if (managerId is not { } id)
            {
                _ended = true;
                return false;
            }

            if (!_seen.Add(id) || _managers.Count >= MaxLength)
            {
                _broken = true;
                _ended = true;
                return false;
            }

            _managers.Add(id);
            return true;
        }

        public ManagerChain Build() => new(userId, [.. _managers], _broken);
    }
}
