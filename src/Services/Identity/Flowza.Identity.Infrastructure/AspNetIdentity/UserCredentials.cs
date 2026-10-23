using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.Domain.Users.Passwords;
using Microsoft.AspNetCore.Identity;

namespace Flowza.Identity.Infrastructure.AspNetIdentity;

/// <summary>Passwords, lockout and sessions through ASP.NET Core Identity (PBKDF2 hashes, constitution 7.7).</summary>
internal sealed class UserCredentials(UserManager<User> userManager, PasswordPolicy policy, ISessionRevoker sessionRevoker)
    : IUserCredentials
{
    public async Task<Result> SetPasswordAsync(User user, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var check = policy.Check(password, user.Username.Value);
        if (check.IsFailure)
        {
            return check;
        }

        if (await userManager.HasPasswordAsync(user))
        {
            Ensure(await userManager.RemovePasswordAsync(user));
        }

        // AddPassword also gives the user a new security stamp, so sessions with the old password end.
        Ensure(await userManager.AddPasswordAsync(user, password));
        await sessionRevoker.RevokeAsync(user.Id, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(User user, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!await userManager.CheckPasswordAsync(user, currentPassword))
        {
            // A wrong current password counts like a wrong password at login (IAM-07; review finding 7).
            Ensure(await userManager.AccessFailedAsync(user));
            return PasswordErrors.CurrentPasswordWrong;
        }

        Ensure(await userManager.ResetAccessFailedCountAsync(user));
        var check = policy.Check(newPassword, user.Username.Value);
        if (check.IsFailure)
        {
            return check;
        }

        Ensure(await userManager.ChangePasswordAsync(user, currentPassword, newPassword));
        return Result.Success();
    }

    public Task<bool> IsLockedOutAsync(User user, CancellationToken cancellationToken) => userManager.IsLockedOutAsync(user);

    public async Task UnlockAsync(User user, CancellationToken cancellationToken)
    {
        Ensure(await userManager.SetLockoutEndDateAsync(user, null));
        Ensure(await userManager.ResetAccessFailedCountAsync(user));
    }

    public async Task EndSessionsAsync(User user, CancellationToken cancellationToken)
    {
        // A new security stamp ends cookie sessions at their next check; revoking ends refresh and access tokens.
        Ensure(await userManager.UpdateSecurityStampAsync(user));
        await sessionRevoker.RevokeAsync(user.Id, cancellationToken);
    }

    // The checks above run first, so a failure here is unexpected (constitution: exceptions only for the unexpected).
    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"ASP.NET Core Identity refused a credential change: {string.Join(", ", result.Errors.Select(e => e.Code))}.");
        }
    }
}

/// <summary>Ends a user's tokens (OpenIddict, task T013).</summary>
internal interface ISessionRevoker
{
    Task RevokeAsync(Guid userId, CancellationToken cancellationToken);
}
