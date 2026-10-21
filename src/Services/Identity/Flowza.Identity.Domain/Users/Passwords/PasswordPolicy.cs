using Flowza.Common.Results;

namespace Flowza.Identity.Domain.Users.Passwords;

/// <summary>Known, often-used passwords (plan 001 PA7: an embedded public top-10,000 list).</summary>
public interface ICommonPasswordList
{
    bool Contains(string password);
}

/// <summary>
/// The rules a new password must follow (IAM-06, K2). M1 uses fixed defaults: at least 10 characters, not a common
/// password, not the username. No character-class rules: length and the common list protect better and are easier
/// to live with. In M3 the minimum length becomes an installation setting (task T040).
/// </summary>
public sealed class PasswordPolicy(ICommonPasswordList commonPasswords)
{
    public const int DefaultMinimumLength = 10;

    /// <summary>Name of the form field the errors belong to.</summary>
    public const string Field = "newPassword";

    public int MinimumLength { get; } = DefaultMinimumLength;

    /// <summary>Checks a new password; the error lists every rule it breaks.</summary>
    public Result Check(string? password, string username)
    {
        ArgumentNullException.ThrowIfNull(username);
        password ??= string.Empty;

        var broken = new List<Error>();
        if (password.Length < MinimumLength)
        {
            broken.Add(PasswordErrors.TooShort(MinimumLength));
        }

        if (password.Length > 0 && commonPasswords.Contains(password))
        {
            broken.Add(PasswordErrors.Common);
        }

        if (string.Equals(password.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            broken.Add(PasswordErrors.SameAsUsername);
        }

        if (broken.Count == 0)
        {
            return Result.Success();
        }

        return Error.Validation(
            broken[0].Code,
            string.Join(" ", broken.Select(e => e.Message)),
            new Dictionary<string, string[]> { [Field] = [.. broken.Select(e => e.Code)] });
    }
}

/// <summary>Password failures; codes are translation keys, messages English fallbacks (never the password itself).</summary>
public static class PasswordErrors
{
    public static Error Common { get; } =
        Error.Validation("passwords.common", "This password is too common. Choose another one.");

    public static Error SameAsUsername { get; } =
        Error.Validation("passwords.same_as_username", "The password must not be the username.");

    public static Error CurrentPasswordWrong { get; } = Error.Validation(
        "passwords.current_wrong",
        "The current password is not correct.",
        new Dictionary<string, string[]> { ["currentPassword"] = ["passwords.current_wrong"] });

    public static Error TooShort(int minimumLength) =>
        Error.Validation("passwords.too_short", $"A password has at least {minimumLength} characters.");
}
