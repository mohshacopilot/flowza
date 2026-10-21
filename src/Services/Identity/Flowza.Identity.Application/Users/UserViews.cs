using Flowza.Common.Paging;

namespace Flowza.Identity.Application.Users;

/// <summary>Status shown in the user list (spec key entity "User": active, inactive or locked).</summary>
public enum UserStatus
{
    Active,
    Inactive,

    /// <summary>Active, but locked for a while after wrong passwords (IAM-07).</summary>
    Locked,
}

/// <summary>One row of the users list (plan 001 §5.3).</summary>
public sealed record UserListItem(
    Guid Id,
    string Username,
    string DisplayName,
    string Email,
    IReadOnlyList<string> Roles,
    UserStatus Status);

/// <summary>A user's details; <see cref="Version"/> must be sent back with an update (constitution 8.5).</summary>
public sealed record UserDetails(
    Guid Id,
    string Username,
    string DisplayName,
    string Email,
    string? Mobile,
    IReadOnlyList<string> Roles,
    UserStatus Status,
    bool MustChangePassword,
    string Version);

public sealed record UserListFilter(string? Search, UserStatus? Status);

/// <summary>Read side of users (CQRS-lite): no-tracking projections shaped for the screens.</summary>
public interface IUserReadStore
{
    Task<PagedResult<UserListItem>> ListAsync(UserListFilter filter, PageRequest page, CancellationToken cancellationToken);

    Task<UserDetails?> GetAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>The row version travels as base64 text.</summary>
public static class UserVersion
{
    public static string Encode(byte[]? rowVersion) => rowVersion is null ? string.Empty : Convert.ToBase64String(rowVersion);

    public static byte[]? Decode(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var buffer = new byte[version.Length];
        return Convert.TryFromBase64String(version, buffer, out var written) && written == 8 ? buffer[..written] : null;
    }
}
