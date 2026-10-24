namespace Flowza.Identity.Domain.Organization;

/// <summary>One unit on the way up: its ID, its head (if any) and whether that head is active.</summary>
public sealed record UnitHead(Guid UnitId, Guid? HeadUserId, bool HeadIsActive);

/// <summary>Why the walk went past a unit.</summary>
public enum HeadSkipReason
{
    /// <summary>The unit has no head (O3).</summary>
    Vacant,

    /// <summary>The head is deactivated; rules skip inactive users (O13).</summary>
    HeadInactive,

    /// <summary>The head is the requester; nobody approves their own request (O5, P4).</summary>
    HeadIsRequester,
}

public sealed record HeadSkip(Guid UnitId, HeadSkipReason Reason);

/// <summary>
/// Where the walk stopped. <see cref="UnitId"/> and <see cref="HeadUserId"/> are null when no unit up to the root has an
/// active head who is not the requester (ORG-06-AC-d5). <see cref="Skipped"/> lists every unit passed, in order, for
/// the explanation.
/// </summary>
public sealed record HeadWalkResult(Guid? UnitId, Guid? HeadUserId, IReadOnlyList<HeadSkip> Skipped)
{
    public bool Found => HeadUserId is not null;
}

/// <summary>
/// The shared "walk up" step of approver rules d and e (spec 002 §4.1; O3, O5, O13, P4): from the start unit up to the
/// root, the first unit whose head is active and is not the requester. Pure; the caller loads the chain (from the start
/// unit's path) once. Cost: O(depth).
/// </summary>
public static class HeadWalk
{
    /// <param name="unitsUpward">The start unit first, then each unit above it, the root last.</param>
    /// <param name="requesterId">The user the rule is resolved for.</param>
    public static HeadWalkResult FindHead(IReadOnlyList<UnitHead> unitsUpward, Guid requesterId)
    {
        ArgumentNullException.ThrowIfNull(unitsUpward);
        var skipped = new List<HeadSkip>();
        foreach (var unit in unitsUpward)
        {
            var reason = unit switch
            {
                { HeadUserId: null } => HeadSkipReason.Vacant,
                { HeadIsActive: false } => HeadSkipReason.HeadInactive,
                _ when unit.HeadUserId == requesterId => HeadSkipReason.HeadIsRequester,
                _ => (HeadSkipReason?)null,
            };

            if (reason is null)
            {
                return new HeadWalkResult(unit.UnitId, unit.HeadUserId, skipped);
            }

            skipped.Add(new HeadSkip(unit.UnitId, reason.Value));
        }

        return new HeadWalkResult(null, null, skipped);
    }
}
