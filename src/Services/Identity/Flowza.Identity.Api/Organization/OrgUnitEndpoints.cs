using Flowza.Common.Paging;
using Flowza.Common.Results;
using Flowza.Identity.Application.Organization.Units;
using Flowza.Identity.Application.Organization.Units.Create;
using Flowza.Identity.Application.Organization.Units.GetTree;
using Flowza.Identity.Application.Organization.Units.GetUnit;
using Flowza.Identity.Application.Organization.Units.ListUnitUsers;
using Flowza.Identity.Application.Organization.Units.Rename;
using Flowza.Identity.Application.Organization.Units.SetHead;
using Flowza.ServiceDefaults.Http;
using Flowza.ServiceDefaults.Security;
using Wolverine;

namespace Flowza.Identity.Api.Organization;

public sealed record CreateOrgUnitRequest(Guid? ParentId, string? Code, string? Name, Guid? UnitTypeId);

public sealed record RenameOrgUnitRequest(string? Name, string? Version);

/// <summary>The new head, or null to make the unit vacant.</summary>
public sealed record SetOrgUnitHeadRequest(Guid? HeadUserId, string? Version);

/// <summary>Org units for Platform Admins (plan 002 §5.4; ORG-02, ORG-03). Each endpoint sends one command or query.</summary>
public static class OrgUnitEndpoints
{
    public static IEndpointRouteBuilder MapOrgUnitEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var units = app.MapGroup("/api/v1/org-units")
            .RequireAuthorization(FlowzaPolicies.PlatformAdmin)
            .WithTags("Org units");

        units.MapGet("/tree", async (IMessageBus bus, CancellationToken ct) =>
            TypedResults.Ok(await bus.InvokeAsync<IReadOnlyList<OrgTreeItem>>(new GetOrgTreeQuery(), ct)))
            .WithName("GetOrgTree");
        units.MapGet("/{id:guid}", async (Guid id, IMessageBus bus, CancellationToken ct) =>
            (await bus.InvokeAsync<Result<OrgUnitDetails>>(new GetOrgUnitQuery(id), ct)).ToHttpResult())
            .WithName("GetOrgUnit")
            .Produces<OrgUnitDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        units.MapGet("/{id:guid}/users", async (Guid id, IMessageBus bus, CancellationToken ct, int page = 1, int size = PageRequest.DefaultSize) =>
            (await bus.InvokeAsync<Result<PagedResult<UnitUserItem>>>(new ListOrgUnitUsersQuery(id, page, size), ct)).ToHttpResult())
            .WithName("ListOrgUnitUsers")
            .Produces<PagedResult<UnitUserItem>>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        units.MapPost("/", CreateAsync).WithName("CreateOrgUnit")
            .Produces<CreatedOrgUnit>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        units.MapPut("/{id:guid}", async (Guid id, RenameOrgUnitRequest body, IMessageBus bus, CancellationToken ct) =>
            (await bus.InvokeAsync<Result>(new RenameOrgUnitCommand(id, body.Name, body.Version), ct)).ToHttpResult())
            .WithName("RenameOrgUnit")
            .ProducesChange();
        units.MapPut("/{id:guid}/head", async (Guid id, SetOrgUnitHeadRequest body, IMessageBus bus, CancellationToken ct) =>
            (await bus.InvokeAsync<Result>(new SetOrgUnitHeadCommand(id, body.HeadUserId, body.Version), ct)).ToHttpResult())
            .WithName("SetOrgUnitHead")
            .ProducesChange()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        return app;
    }

    private static async Task<IResult> CreateAsync(CreateOrgUnitRequest body, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<Result<CreatedOrgUnit>>(
            new CreateOrgUnitCommand(body.ParentId, body.Code, body.Name, body.UnitTypeId),
            cancellationToken);
        return result.IsFailure
            ? result.Error.ToProblem()
            : TypedResults.Created($"/api/v1/org-units/{result.Value.Id}", result.Value);
    }
}
