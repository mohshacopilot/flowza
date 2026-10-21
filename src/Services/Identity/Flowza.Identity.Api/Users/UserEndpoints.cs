using Flowza.Common.Paging;
using Flowza.Common.Results;
using Flowza.Identity.Application.Users;
using Flowza.Identity.Application.Users.ChangeUserStatus;
using Flowza.Identity.Application.Users.CreateUser;
using Flowza.Identity.Application.Users.GetUser;
using Flowza.Identity.Application.Users.ListUsers;
using Flowza.Identity.Application.Users.ResetPassword;
using Flowza.Identity.Application.Users.SetUserRoles;
using Flowza.Identity.Application.Users.UpdateUser;
using Flowza.ServiceDefaults.Http;
using Flowza.ServiceDefaults.Security;
using Wolverine;

namespace Flowza.Identity.Api.Users;

public sealed record CreateUserRequest(string? Username, string? DisplayName, string? Email, string? Mobile, IReadOnlyList<string>? Roles);

public sealed record UpdateUserRequest(string? DisplayName, string? Email, string? Mobile, string? Version);

public sealed record SetUserRolesRequest(IReadOnlyList<string>? Roles);

/// <summary>User administration for Platform Admins (plan 001 §5.3; IAM-02, IAM-04, IAM-07). Each endpoint sends one command or query.</summary>
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var users = app.MapGroup("/api/v1/users")
            .RequireAuthorization(FlowzaPolicies.PlatformAdmin)
            .WithTags("Users");

        users.MapGet("/", ListAsync).WithName("ListUsers");
        users.MapPost("/", CreateAsync).WithName("CreateUser");
        users.MapGet("/{id:guid}", async (Guid id, IMessageBus bus, CancellationToken ct) =>
            (await bus.InvokeAsync<Result<UserDetails>>(new GetUserQuery(id), ct)).ToHttpResult()).WithName("GetUser");
        users.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest body, IMessageBus bus, CancellationToken ct) =>
            (await bus.InvokeAsync<Result>(new UpdateUserCommand(id, body.DisplayName, body.Email, body.Mobile, body.Version), ct)).ToHttpResult())
            .WithName("UpdateUser");
        users.MapPut("/{id:guid}/roles", async (Guid id, SetUserRolesRequest body, IMessageBus bus, CancellationToken ct) =>
            (await bus.InvokeAsync<Result>(new SetUserRolesCommand(id, body.Roles), ct)).ToHttpResult()).WithName("SetUserRoles");
        users.MapPost("/{id:guid}/deactivate", async (Guid id, IMessageBus bus, CancellationToken ct) =>
            (await bus.InvokeAsync<Result>(new DeactivateUserCommand(id), ct)).ToHttpResult()).WithName("DeactivateUser");
        users.MapPost("/{id:guid}/activate", async (Guid id, IMessageBus bus, CancellationToken ct) =>
            (await bus.InvokeAsync<Result>(new ActivateUserCommand(id), ct)).ToHttpResult()).WithName("ActivateUser");
        users.MapPost("/{id:guid}/reset-password", ResetPasswordAsync).WithName("ResetUserPassword");
        users.MapPost("/{id:guid}/unlock", async (Guid id, IMessageBus bus, CancellationToken ct) =>
            (await bus.InvokeAsync<Result>(new UnlockUserCommand(id), ct)).ToHttpResult()).WithName("UnlockUser");
        return app;
    }

    private static async Task<IResult> ListAsync(
        IMessageBus bus,
        CancellationToken cancellationToken,
        int page = 1,
        int size = PageRequest.DefaultSize,
        string? search = null,
        string? status = null)
    {
        UserStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<UserStatus>(status, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return Error.Validation(
                    "users.status_unknown",
                    "Status is one of active, inactive or locked.",
                    new Dictionary<string, string[]> { ["status"] = ["users.status_unknown"] }).ToProblem();
            }

            statusFilter = parsed;
        }

        var result = await bus.InvokeAsync<PagedResult<UserListItem>>(new ListUsersQuery(page, size, search, statusFilter), cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> CreateAsync(CreateUserRequest body, IMessageBus bus, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<Result<CreatedUser>>(
            new CreateUserCommand(body.Username, body.DisplayName, body.Email, body.Mobile, body.Roles),
            cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        // The temporary password is shown once (K6): never cached by browsers or proxies.
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Created($"/api/v1/users/{result.Value.Id}", result.Value);
    }

    private static async Task<IResult> ResetPasswordAsync(Guid id, IMessageBus bus, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<Result<TemporaryPassword>>(new ResetPasswordCommand(id), cancellationToken);
        if (result.IsSuccess)
        {
            context.Response.Headers.CacheControl = "no-store";
        }

        return result.ToHttpResult();
    }
}
