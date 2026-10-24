using System.Text.Json.Serialization;

namespace Flowza.Contracts.Identity.ApproverRules.V1;

// The approver rule contract (plan 002 §7, PA2), shared by Design (stores rules in process designs), Runtime (asks for
// approvers) and Identity (resolves them). Rules refer to unit types and roles by code and to users by username, both
// fixed after creation (O9, P8). A released shape never changes; a new one gets a new version namespace (constitution 3.5).

/// <summary>
/// Who should act, as stored in a process design: one of the rules a–g of spec 002 §4.1. In JSON the rule type is the
/// <c>type</c> property (written first), e.g. <c>{ "type": "NearestUnitOfTypeHead", "unitType": "ZONE" }</c>; an unknown
/// type is refused when read.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(SpecificUserRule), SpecificUserRule.Type)]
[JsonDerivedType(typeof(RequesterRule), RequesterRule.Type)]
[JsonDerivedType(typeof(ManagerLevelsUpRule), ManagerLevelsUpRule.Type)]
[JsonDerivedType(typeof(RequesterUnitHeadRule), RequesterUnitHeadRule.Type)]
[JsonDerivedType(typeof(NearestUnitOfTypeHeadRule), NearestUnitOfTypeHeadRule.Type)]
[JsonDerivedType(typeof(RoleInScopeRule), RoleInScopeRule.Type)]
[JsonDerivedType(typeof(UserPickerFieldRule), UserPickerFieldRule.Type)]
public abstract record ApproverRule;

/// <summary>Rule a: the user with this username.</summary>
public sealed record SpecificUserRule(string Username) : ApproverRule
{
    public const string Type = "SpecificUser";
}

/// <summary>Rule b: the requester themselves.</summary>
public sealed record RequesterRule : ApproverRule
{
    public const string Type = "Requester";
}

/// <summary>Rule c: the reporting manager <paramref name="Levels"/> steps up (1 = the requester's own manager).</summary>
public sealed record ManagerLevelsUpRule(int Levels) : ApproverRule
{
    public const string Type = "ManagerLevelsUp";
}

/// <summary>Rule d: the head of the requester's primary unit.</summary>
public sealed record RequesterUnitHeadRule : ApproverRule
{
    public const string Type = "RequesterUnitHead";
}

/// <summary>Rule e: the head of the nearest unit of this unit type, at or above the requester's unit.</summary>
public sealed record NearestUnitOfTypeHeadRule(string UnitType) : ApproverRule
{
    public const string Type = "NearestUnitOfTypeHead";
}

/// <summary>
/// Rule f: the active users with this business role in the scope; <paramref name="UnitType"/> only for
/// <see cref="RoleScope.NearestUnitOfTypeAndBelow"/>.
/// </summary>
public sealed record RoleInScopeRule(string Role, RoleScope Scope, string? UnitType = null) : ApproverRule
{
    public const string Type = "RoleInScope";
}

/// <summary>Rule g: the user picked in this user-picker field of the request's form.</summary>
public sealed record UserPickerFieldRule(string Field) : ApproverRule
{
    public const string Type = "UserPickerField";
}

/// <summary>Where rule f looks for role members (O8).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RoleScope>))]
public enum RoleScope
{
    /// <summary>Anywhere in the organization.</summary>
    Anywhere,

    /// <summary>The requester's primary unit only.</summary>
    RequesterUnit,

    /// <summary>The requester's primary unit and every unit below it.</summary>
    RequesterUnitAndBelow,

    /// <summary>The nearest unit of a unit type at or above the requester, and every unit below it.</summary>
    NearestUnitOfTypeAndBelow,
}

/// <summary>
/// A request to resolve <paramref name="Rule"/> for <paramref name="RequesterId"/> with the org data of this moment
/// (APP-06). For rule g the caller reads the form field itself and sends only the picked user's ID (or none), so form
/// values never leave Runtime (constitution 11).
/// </summary>
public sealed record ResolveRequest(Guid RequesterId, ApproverRule Rule, Guid? PickedUserId = null);

/// <summary>
/// The users found (0, 1 or many), the explanation of how, and for an empty result the reason (ORG-06-AC-x1, x3).
/// Names in the explanation are as they were at that moment, so the request history shows what was true then (O12).
/// </summary>
public sealed record ResolveResult(IReadOnlyList<Guid> UserIds, IReadOnlyList<ExplanationStep> Explanation, ExplanationStep? Reason);

/// <summary>
/// One readable step, as a translation key and its arguments (NFR-10), e.g. key <c>org.explain.nearestOfType</c> with
/// arguments <c>unit</c> and <c>unitType</c>. <paramref name="Number"/> carries a count where the text needs one
/// (e.g. the levels of rule c).
/// </summary>
public sealed record ExplanationStep(string Key, IReadOnlyDictionary<string, OrgRef> Args, int? Number = null);

/// <summary>A user, unit, unit type or business role named in an explanation, captured at that moment.</summary>
public sealed record OrgRef(OrgRefKind Kind, Guid Id, string? Code, string Name);

[JsonConverter(typeof(JsonStringEnumConverter<OrgRefKind>))]
public enum OrgRefKind
{
    User,
    Unit,
    UnitType,
    BusinessRole,
}
