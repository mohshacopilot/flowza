using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace Flowza.Identity.Infrastructure.AspNetIdentity;

public enum LoginOutcome
{
    Succeeded,

    /// <summary>Unknown user, wrong password or inactive user: one general answer (IAM-01-AC2).</summary>
    InvalidCredentials,

    /// <summary>5 wrong passwords in a row; locked for 15 minutes (IAM-07-AC1).</summary>
    LockedOut,
}

public sealed record LoginCheck(LoginOutcome Outcome, User? User)
{
    public bool MustChangePassword => Outcome == LoginOutcome.Succeeded && User!.MustChangePassword;
}

/// <summary>
/// Checks a username and password and counts wrong passwords towards the lockout (IAM-01, IAM-07). The login page
/// signs the user in only on <see cref="LoginOutcome.Succeeded"/>.
/// </summary>
public sealed class PasswordLogin(
    IUserRepository users,
    SignInManager<User> signInManager,
    IUnitOfWork unitOfWork)
{
    public async Task<LoginCheck> CheckAsync(string? username, string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return new LoginCheck(LoginOutcome.InvalidCredentials, null);
        }

        var user = await users.FindByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            return new LoginCheck(LoginOutcome.InvalidCredentials, null);
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            // Two logins of the same user at the same moment; the other one's counters won. Ask to try again.
            return new LoginCheck(LoginOutcome.InvalidCredentials, user);
        }

        if (result.IsLockedOut)
        {
            return new LoginCheck(LoginOutcome.LockedOut, user);
        }

        // An inactive user gets the same answer as a wrong password, so the page reveals nothing (IAM-01-AC2).
        return result.Succeeded && user.IsActive
            ? new LoginCheck(LoginOutcome.Succeeded, user)
            : new LoginCheck(LoginOutcome.InvalidCredentials, user);
    }
}
