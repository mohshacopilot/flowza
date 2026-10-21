namespace Flowza.Contracts.Identity.Events.V1;

// Integration events of the Identity service (plan 001 §7, PA2). IDs only: never names, emails or passwords
// (constitution 11.2). Each also carries EventId, OccurredAt, ActorId and CorrelationId from IntegrationEvent.

/// <summary>An admin (or the installation tool) created a user.</summary>
public sealed record UserCreated(Guid UserId) : IntegrationEvent;

/// <summary>Display name, email or mobile changed.</summary>
public sealed record UserUpdated(Guid UserId) : IntegrationEvent;

/// <summary>The user's system roles now are <paramref name="Roles"/> (role names, e.g. "AppDesigner").</summary>
public sealed record UserRolesChanged(Guid UserId, IReadOnlyList<string> Roles) : IntegrationEvent;

/// <summary>The user may no longer act; services refuse their next request (deny list, K7).</summary>
public sealed record UserDeactivated(Guid UserId) : IntegrationEvent;

/// <summary>The user may act again (C6).</summary>
public sealed record UserActivated(Guid UserId) : IntegrationEvent;

public sealed record UserLoggedIn(Guid UserId) : IntegrationEvent;

/// <summary>A login of an existing user was refused (wrong password, locked or inactive). Unknown usernames publish nothing.</summary>
public sealed record UserLoginFailed(Guid UserId) : IntegrationEvent;

/// <summary>Too many wrong passwords: the account is locked for a while (IAM-07).</summary>
public sealed record UserLockedOut(Guid UserId) : IntegrationEvent;

/// <summary>The user chose a new password themselves.</summary>
public sealed record UserPasswordChanged(Guid UserId) : IntegrationEvent;

/// <summary>An admin gave the user a temporary password.</summary>
public sealed record UserPasswordReset(Guid UserId) : IntegrationEvent;
