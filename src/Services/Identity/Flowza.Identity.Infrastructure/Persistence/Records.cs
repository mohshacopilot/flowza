namespace Flowza.Identity.Infrastructure.Persistence;

/// <summary>A row of the roles table, seeded from <c>SystemRole.All</c> (plan 001 §5.2).</summary>
public sealed class RoleRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;
}

/// <summary>
/// A former password hash of a user. The table exists from feature 001 so that the reuse rule of M3 (IAM-06-AC2)
/// needs no migration of a busy table later; it is written from M3 (task T040).
/// </summary>
public sealed class PasswordHistoryRecord
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset ChangedAt { get; set; }
}

/// <summary>A Data Protection key, kept in the database so cookies and tokens survive restarts and work on several instances.</summary>
public sealed class DataProtectionKeyRecord
{
    public int Id { get; set; }

    public string? FriendlyName { get; set; }

    public string Xml { get; set; } = string.Empty;
}
