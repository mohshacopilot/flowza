using Flowza.Common.Results;

namespace Flowza.Identity.Domain.Users;

/// <summary>
/// Expected failures of user rules. Codes are stable translation keys for the web app; messages are English
/// fallbacks and never quote the value that was entered (constitution 11.2).
/// </summary>
public static class UserErrors
{
    public static Error InvalidUsername { get; } = Field(
        "username",
        "users.username_invalid",
        $"A username has {Username.MinLength}–{Username.MaxLength} characters: letters, digits, '.', '_' or '-'.");

    public static Error InvalidEmail { get; } = Field("email", "users.email_invalid", "Enter a valid email address.");

    public static Error InvalidDisplayName { get; } = Field(
        "displayName",
        "users.display_name_invalid",
        $"A display name is required and has at most {User.DisplayNameMaxLength} characters.");

    public static Error InvalidMobile { get; } = Field(
        "mobile",
        "users.mobile_invalid",
        $"A mobile number has at most {User.MobileMaxLength} characters: digits, spaces, '+', '-', '(' or ')'.");

    public static Error UsernameTaken { get; } = Field("username", "users.username_taken", "This username is already used.");

    public static Error EmailTaken { get; } = Field("email", "users.email_taken", "This email address is already used.");

    /// <summary>L3: removing the last active Platform Admin would lock everyone out of administration.</summary>
    public static Error LastActivePlatformAdmin { get; } = Error.Conflict(
        "users.last_active_platform_admin",
        "The last active Platform Admin cannot be deactivated or lose the Platform Admin role.");

    public static Error NotFound { get; } = Error.NotFound("users.not_found", "The user was not found.");

    private static Error Field(string field, string code, string message) =>
        Error.Validation(code, message, new Dictionary<string, string[]> { [field] = [code] });
}
