using Flowza.Common.Data;

namespace Flowza.Identity.Domain.Organization;

/// <summary>
/// One row whose row version guards the rules that span several records (plan 002 §5.1): "never under its own
/// subtree", "an active unit never under an inactive one", "an active user's unit is active" and "no reporting loop".
/// Every change those rules depend on touches this row in its transaction, so two such changes at the same moment
/// cannot both pass their checks on stale data: the second gets the usual "changed by someone else" conflict
/// (constitution 8.5) and tries again. Ordinary edits (names, heads, roles) do not touch it.
/// </summary>
public sealed class OrgStructure : IVersioned
{
    /// <summary>The ID of the single row (seeded by the migration).</summary>
    public static readonly Guid SingletonId = new("0f6e5600-0000-4000-8000-000000000001");

    public OrgStructure(Guid id) => Id = id;

    public Guid Id { get; private set; }

    /// <summary>Counts the structural changes; always changes on <see cref="Touch"/>, so the row is always written.</summary>
    public long ChangeCount { get; private set; }

    public DateTimeOffset? ChangedAt { get; private set; }

    public void Touch(DateTimeOffset now)
    {
        ChangeCount++;
        ChangedAt = now.ToUniversalTime();
    }
}
