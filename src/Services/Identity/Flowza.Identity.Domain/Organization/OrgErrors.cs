using Flowza.Common.Results;

namespace Flowza.Identity.Domain.Organization;

/// <summary>
/// Expected failures of the organization rules (spec 002). Codes are stable translation keys for the web app; messages
/// are English fallbacks and never quote the value that was entered (constitution 11.2).
/// </summary>
public static class OrgErrors
{
    public static Error InvalidCode { get; } = Field(
        "code",
        "org.code_invalid",
        $"A code has 1–{OrgCode.MaxLength} characters: letters, digits, '-' or '_'.");

    public static Error InvalidName { get; } = Field(
        "name",
        "org.name_invalid",
        $"A name is required and has at most {OrgName.MaxLength} characters.");

    // Rule refusals are Failure (HTTP 422, plan 002 §5.4); Conflict (409) stays for "changed by someone else".

    /// <summary>P9: a unit type stays active while active units use it.</summary>
    public static Error UnitTypeInUse { get; } = Error.Failure(
        "org.unit_type_in_use",
        "This unit type is used by active units. Change or deactivate those units first.");

    /// <summary>ORG-01-AC4, P9: a deactivated unit type cannot be chosen for a new unit.</summary>
    public static Error UnitTypeInactive { get; } = Error.Failure(
        "org.unit_type_inactive",
        "This unit type is deactivated and cannot be chosen for a new unit.");

    /// <summary>C4, C3: an active unit never sits under an inactive one.</summary>
    public static Error ParentInactive { get; } = Error.Failure(
        "org.parent_inactive",
        "The unit above is deactivated. Choose an active unit, or activate that unit first.");

    /// <summary>ORG-02-AC2: a unit cannot move under itself or under a unit below it.</summary>
    public static Error MoveIntoOwnSubtree { get; } = Error.Failure(
        "org.move_into_own_subtree",
        "A unit cannot be moved under itself or under one of the units below it.");

    /// <summary>The root is the top of the tree (C1, ORG-02-AC5).</summary>
    public static Error RootCannotMove { get; } = Error.Failure(
        "org.root_cannot_move",
        "The top unit of the organization cannot be moved.");

    /// <summary>PA4: the tree has at most <see cref="UnitPath.MaxDepth"/> levels.</summary>
    public static Error TreeTooDeep { get; } = Error.Failure(
        "org.tree_too_deep",
        $"The organization can have at most {UnitPath.MaxDepth} levels.");

    /// <summary>O2, ORG-02-AC4: move or deactivate the active child units and users first.</summary>
    public static Error UnitHasActiveMembers { get; } = Error.Failure(
        "org.unit_has_active_members",
        "This unit still has active units or active users. Move or deactivate them first.");

    /// <summary>ORG-03-AC2: a unit's head must be an active user.</summary>
    public static Error HeadInactive { get; } = Error.Failure(
        "org.head_inactive",
        "The head of a unit must be an active user.");

    /// <summary>ORG-04-AC1, C2: a user's primary unit is active (also needed to re-activate the user).</summary>
    public static Error PrimaryUnitInactive { get; } = Field(
        "primaryUnitId",
        "org.primary_unit_inactive",
        "The primary unit must be an active unit.");

    /// <summary>ORG-04-AC3.</summary>
    public static Error ManagerIsSelf { get; } = Field(
        "managerId",
        "org.manager_is_self",
        "A user cannot be their own reporting manager.");

    /// <summary>C7, ORG-04-AC7.</summary>
    public static Error ManagerInactive { get; } = Field(
        "managerId",
        "org.manager_inactive",
        "The reporting manager must be an active user.");

    /// <summary>ORG-04-AC2: the user already appears above the chosen manager.</summary>
    public static Error ReportingLoop { get; } = Field(
        "managerId",
        "org.reporting_loop",
        "This manager reports to the user, directly or through others. Choose another manager.");

    /// <summary>P10, ORG-05-AC2: a deactivated role cannot be given to users.</summary>
    public static Error BusinessRoleInactive { get; } = Error.Failure(
        "org.business_role_inactive",
        "This business role is deactivated and cannot be given to users.");

    /// <summary>C7, ORG-05-AC5: only active users receive business roles.</summary>
    public static Error MemberInactive { get; } = Error.Failure(
        "org.member_inactive",
        "Only active users can be given a business role.");

    private static Error Field(string field, string code, string message) =>
        Error.Validation(code, message, new Dictionary<string, string[]> { [field] = [code] });
}
