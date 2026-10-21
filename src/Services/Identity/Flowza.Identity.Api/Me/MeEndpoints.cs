using Flowza.Common.Results;
using Flowza.Identity.Application.Me.ChangeOwnPassword;
using Flowza.Identity.Application.Me.GetMyProfile;
using Flowza.ServiceDefaults.Http;
using OpenIddict.Abstractions;
using Wolverine;

namespace Flowza.Identity.Api.Me;

public sealed record ChangeMyPasswordRequest(string? CurrentPassword, string? NewPassword);

/// <summary>What every logged-in user may do for themselves (L4; IAM-06-AC3).</summary>
public static class MeEndpoints
{
    /// <summary>A service's own token (client credentials) is not a user.</summary>
    public static Error NotAUser { get; } = Error.Forbidden("me.not_a_user", "This token does not belong to a user.");

    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var me = app.MapGroup("/api/v1/me").RequireAuthorization().WithTags("Me");

        me.MapGet("/", async (HttpContext context, IMessageBus bus, CancellationToken ct) =>
            UserIdOf(context) is { } userId
                ? (await bus.InvokeAsync<Result<MyProfile>>(new GetMyProfileQuery(userId), ct)).ToHttpResult()
                : NotAUser.ToProblem())
            .WithName("GetMyProfile");

        me.MapPost("/password", async (ChangeMyPasswordRequest body, HttpContext context, IMessageBus bus, CancellationToken ct) =>
            UserIdOf(context) is { } userId
                ? (await bus.InvokeAsync<Result>(new ChangeOwnPasswordCommand(userId, body.CurrentPassword, body.NewPassword), ct)).ToHttpResult()
                : NotAUser.ToProblem())
            .WithName("ChangeMyPassword");

        return app;
    }

    private static Guid? UserIdOf(HttpContext context) =>
        Guid.TryParse(context.User.GetClaim(OpenIddictConstants.Claims.Subject), out var id) ? id : null;
}
