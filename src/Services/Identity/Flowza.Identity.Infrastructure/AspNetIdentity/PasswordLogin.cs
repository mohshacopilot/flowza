using System.Diagnostics.Metrics;
using Flowza.Contracts.Identity.Events.V1;
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
/// signs the user in only on <see cref="LoginOutcome.Succeeded"/>. Every attempt of an existing user publishes an
/// event with IDs only (IAM-L5); attempts with an unknown username only count in a metric (no personal data, plan §7).
/// </summary>
public sealed class PasswordLogin(
    IUserRepository users,
    IUserCredentials credentials,
    SignInManager<User> signInManager,
    IUnitOfWork unitOfWork,
    LoginMetrics metrics)
{
    public async Task<LoginCheck> CheckAsync(string? username, string? password, CancellationToken cancellationToken)
    {
        var user = string.IsNullOrWhiteSpace(username) ? null : await users.FindByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            metrics.UnknownUsername();
            return new LoginCheck(LoginOutcome.InvalidCredentials, null);
        }

        var wasLockedOut = await credentials.IsLockedOutAsync(user, cancellationToken);
        var result = await signInManager.CheckPasswordSignInAsync(user, password ?? string.Empty, lockoutOnFailure: true);
        var outcome = result.IsLockedOut ? LoginOutcome.LockedOut
            : result.Succeeded && user.IsActive ? LoginOutcome.Succeeded
            : LoginOutcome.InvalidCredentials;

        if (outcome == LoginOutcome.Succeeded)
        {
            unitOfWork.AddEvent(new UserLoggedIn(user.Id));
        }
        else
        {
            unitOfWork.AddEvent(new UserLoginFailed(user.Id));
            if (outcome == LoginOutcome.LockedOut && !wasLockedOut)
            {
                unitOfWork.AddEvent(new UserLockedOut(user.Id));
            }
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            // Two logins of the same user at the same moment; the other one's counters won. Ask to try again.
            return new LoginCheck(LoginOutcome.InvalidCredentials, user);
        }

        // An inactive user gets the same answer as a wrong password, so the page reveals nothing (IAM-01-AC2).
        return new LoginCheck(outcome, user);
    }
}

/// <summary>Login counters (OpenTelemetry metrics, tech discussion OB3).</summary>
public sealed class LoginMetrics : IDisposable
{
    public const string MeterName = "Flowza.Identity";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _unknownUsername;

    public LoginMetrics() =>
        _unknownUsername = _meter.CreateCounter<long>("flowza.identity.login.unknown_username", description: "Login attempts with a username that does not exist");

    public void UnknownUsername() => _unknownUsername.Add(1);

    public void Dispose() => _meter.Dispose();
}
