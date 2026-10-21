namespace Flowza.Identity.Infrastructure.Persistence;

/// <summary>
/// Columns of the users table that only the infrastructure knows (plan 001 §5.2): password hash, security stamp and
/// lockout counters. They are EF Core shadow properties, so the domain model never sees a password hash.
/// </summary>
public static class UserCredentialColumns
{
    public const string PasswordHash = nameof(PasswordHash);
    public const string SecurityStamp = nameof(SecurityStamp);
    public const string AccessFailedCount = nameof(AccessFailedCount);
    public const string LockoutEnd = nameof(LockoutEnd);
    public const string LockoutEnabled = nameof(LockoutEnabled);
}
