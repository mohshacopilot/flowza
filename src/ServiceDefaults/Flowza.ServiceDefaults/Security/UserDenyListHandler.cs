using System.Diagnostics.CodeAnalysis;
using Flowza.Contracts.Identity.Events.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Validation;
using static OpenIddict.Validation.OpenIddictValidationEvents;

namespace Flowza.ServiceDefaults.Security;

/// <summary>
/// Keeps the deny list up to date from Identity's events (Wolverine handler, discovered in every service). Safe to run
/// twice: the same event sets the same state (constitution 3.4).
/// </summary>
public static class UserDenyListHandler
{
    public static void Handle(UserDeactivated deactivated, IUserDenyList denyList)
    {
        ArgumentNullException.ThrowIfNull(deactivated);
        ArgumentNullException.ThrowIfNull(denyList);
        denyList.Deny(deactivated.UserId, deactivated.OccurredAt);
    }

    public static void Handle(UserActivated activated, IUserDenyList denyList)
    {
        ArgumentNullException.ThrowIfNull(activated);
        ArgumentNullException.ThrowIfNull(denyList);
        denyList.Allow(activated.UserId, activated.OccurredAt);
    }
}

/// <summary>
/// OpenIddict validation step (named without "Handler", so Wolverine does not take it for a message handler).
/// Refuses the access token of a user on the deny list, as if the token were invalid: the caller gets 401 and must log
/// in again, which a deactivated user cannot (IAM-04-AC3).
/// </summary>
public sealed class RejectDeniedUsers(IUserDenyList denyList) : IOpenIddictValidationHandler<ValidateTokenContext>
{
    /// <summary>Runs after OpenIddict has validated the token and built its principal.</summary>
    public static OpenIddictValidationHandlerDescriptor Descriptor { get; } =
        OpenIddictValidationHandlerDescriptor.CreateBuilder<ValidateTokenContext>()
            .UseSingletonHandler<RejectDeniedUsers>()
            .SetOrder(int.MaxValue - 100_000)
            .SetType(OpenIddictValidationHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(ValidateTokenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Guid.TryParse(context.Principal?.GetClaim(OpenIddictConstants.Claims.Subject), out var userId) && denyList.IsDenied(userId))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken, "The user is no longer active.");
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>Fills the deny list at start-up from the service's <see cref="IUserDenyListSource"/>, if it has one.</summary>
internal sealed partial class UserDenyListLoader(
    IServiceScopeFactory scopeFactory,
    IUserDenyList denyList,
    TimeProvider timeProvider,
    ILogger<UserDenyListLoader> logger) : IHostedService
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failed load must not stop the service; events keep the list current.")]
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sources = scope.ServiceProvider.GetServices<IUserDenyListSource>().ToList();
        if (sources.Count == 0)
        {
            return;
        }

        // Older than any event that may still arrive, so a newer activation event wins.
        var loadedAt = timeProvider.GetUtcNow().AddDays(-1);
        try
        {
            foreach (var source in sources)
            {
                foreach (var userId in await source.InactiveUserIdsAsync(cancellationToken))
                {
                    denyList.Deny(userId, loadedAt);
                }
            }
        }
        catch (Exception exception)
        {
            LogLoadFailed(logger, exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "The deny list could not be loaded at start-up; it fills from events")]
    private static partial void LogLoadFailed(ILogger logger, Exception exception);
}
