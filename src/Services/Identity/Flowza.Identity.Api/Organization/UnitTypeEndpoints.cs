using Flowza.Common.Results;
using Flowza.Identity.Application.Organization.UnitTypes;
using Flowza.Identity.Application.Organization.UnitTypes.ChangeStatus;
using Flowza.Identity.Application.Organization.UnitTypes.Create;
using Flowza.Identity.Application.Organization.UnitTypes.List;
using Flowza.Identity.Application.Organization.UnitTypes.Rename;
using Flowza.ServiceDefaults.Http;
using Flowza.ServiceDefaults.Security;
using Wolverine;

namespace Flowza.Identity.Api.Organization;

public sealed record CreateUnitTypeRequest(string? Code, string? Name);

public sealed record RenameUnitTypeRequest(string? Name, string? Version);

/// <summary>The version the admin last read, for a status change (constitution 8.5).</summary>
public sealed record VersionRequest(string? Version);

/// <summary>Unit types for Platform Admins (plan 002 §5.4; ORG-01). Each endpoint sends one command or query.</summary>
public static class UnitTypeEndpoints
{
    public static IEndpointRouteBuilder MapUnitTypeEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var unitTypes = app.MapGroup("/api/v1/unit-types")
            .RequireAuthorization(FlowzaPolicies.PlatformAdmin)
            .WithTags("Unit types");

        unitTypes.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
            TypedResults.Ok(await bus.InvokeAsync<IReadOnlyList<UnitTypeListItem>>(new ListUnitTypesQuery(), ct)))
            .WithName("ListUnitTypes");
        unitTypes.MapPost("/", CreateAsync).WithName("CreateUnitType")
            .Produces<CreatedUnitType>(StatusCodes.Status201Created)
            .ProducesValidationProblem();
        unitTypes.MapPut("/{id:guid}", async (Guid id, RenameUnitTypeRequest body, IMessageBus bus, CancellationToken ct) =>
            (await bus.InvokeAsync<Result>(new RenameUnitTypeCommand(id, body.Name, body.Version), ct)).ToHttpResult())
            .WithName("RenameUnitType")
            .ProducesChange();
        unitTypes.MapPost("/{id:guid}/deactivate", async (Guid id, VersionRequest body, IMessageBus bus, CancellationToken ct) =>
            (await bus.InvokeAsync<Result>(new DeactivateUnitTypeCommand(id, body.Version), ct)).ToHttpResult())
            .WithName("DeactivateUnitType")
            .ProducesChange()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        unitTypes.MapPost("/{id:guid}/activate", async (Guid id, VersionRequest body, IMessageBus bus, CancellationToken ct) =>
            (await bus.InvokeAsync<Result>(new ActivateUnitTypeCommand(id, body.Version), ct)).ToHttpResult())
            .WithName("ActivateUnitType")
            .ProducesChange();
        return app;
    }

    /// <summary>A change answers 204, or ProblemDetails for invalid values, a missing record or a conflict.</summary>
    internal static RouteHandlerBuilder ProducesChange(this RouteHandlerBuilder builder) => builder
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<IResult> CreateAsync(CreateUnitTypeRequest body, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<Result<CreatedUnitType>>(new CreateUnitTypeCommand(body.Code, body.Name), cancellationToken);
        return result.IsFailure
            ? result.Error.ToProblem()
            : TypedResults.Created($"/api/v1/unit-types/{result.Value.Id}", result.Value);
    }
}
