using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Organization;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowza.Identity.IntegrationTests.Organization;

/// <summary>T007 / ORG-02, ORG-03: creating, renaming and reading org units and setting their heads (plan 002 §5.4).</summary>
[Collection(IdentityTestGroup.Name)]
public sealed class OrgUnitsApiTests(IdentityApp app) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ApiSession _admin = null!;

    public async ValueTask InitializeAsync()
    {
        await app.ResetDataAsync();
        (_, _admin) = await app.UserWithSessionAsync("units.admin", SystemRole.PlatformAdmin);
    }

    public ValueTask DisposeAsync()
    {
        _admin.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ORG_02_AC1_units_are_created_at_any_depth_with_any_type_under_any_type()
    {
        var zone = await CreateTypeAsync("ZONE");
        var branch = await CreateTypeAsync("BRANCH");
        var dept = await CreateTypeAsync("DEPT");

        // Five levels below the root, with the types in an order no real tree would use (O1).
        var level1 = await CreateUnitAsync(OrgRoot.UnitId, "L1", branch);
        var level2 = await CreateUnitAsync(level1, "L2", zone);
        var level3 = await CreateUnitAsync(level2, "L3", dept);
        var level4 = await CreateUnitAsync(level3, "L4", zone);
        var level5 = await CreateUnitAsync(level4, "L5", branch);

        var tree = await TreeAsync();
        tree.Select(u => u.GetProperty("id").GetGuid()).ShouldBe([OrgRoot.UnitId, level1, level2, level3, level4, level5]);
        tree.Select(u => u.GetProperty("parentId").ValueKind == JsonValueKind.Null ? (Guid?)null : u.GetProperty("parentId").GetGuid())
            .ShouldBe([null, OrgRoot.UnitId, level1, level2, level3, level4]);

        var deepest = await UnitAsync(level5);
        deepest.GetProperty("code").GetString().ShouldBe("L5");
        deepest.GetProperty("unitTypeCode").GetString().ShouldBe("BRANCH");
        deepest.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        deepest.GetProperty("activeUsers").GetInt32().ShouldBe(0);

        var path = await app.WithDbContextAsync(context => context.OrgUnits.AsNoTracking().SingleAsync(u => u.Id == level5, Ct));
        path.Path.Depth.ShouldBe(6);

        // Creating a unit is a structural change: each one touched the org structure (plan §5.1).
        (await app.WithDbContextAsync(context => context.OrgStructure.AsNoTracking().SingleAsync(Ct))).ChangeCount.ShouldBe(5);
    }

    [Fact]
    public async Task ORG_02_AC1_the_tree_lists_every_parent_before_its_children_with_type_and_head()
    {
        var zone = await CreateTypeAsync("ZONE");
        var south = await CreateUnitAsync(OrgRoot.UnitId, "SOUTH", zone);
        var north = await CreateUnitAsync(OrgRoot.UnitId, "NORTH", zone);
        var chennai = await CreateUnitAsync(south, "CHENNAI", zone);
        var priya = await app.CreateUserAsync("priya.head");
        (await SetHeadAsync(south, priya.Id)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var tree = await TreeAsync();

        var order = tree.Select(u => u.GetProperty("id").GetGuid()).ToList();
        order.IndexOf(OrgRoot.UnitId).ShouldBe(0);
        order.IndexOf(chennai).ShouldBeGreaterThan(order.IndexOf(south));
        order.ShouldContain(north);
        var southItem = tree.Single(u => u.GetProperty("id").GetGuid() == south);
        southItem.GetProperty("unitTypeName").GetString().ShouldBe("ZONE name");
        southItem.GetProperty("headUserId").GetGuid().ShouldBe(priya.Id);
        southItem.GetProperty("headDisplayName").GetString().ShouldBe("Test priya.head");
        southItem.GetProperty("version").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ORG_02_AC8_a_unit_cannot_be_added_under_an_inactive_unit()
    {
        var zone = await CreateTypeAsync("ZONE");
        var closed = await CreateUnitAsync(OrgRoot.UnitId, "CLOSED", zone);
        await DeactivateUnitAsync(closed);

        var response = await PostUnitAsync(closed, "UNDER", zone);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ProblemCodeAsync(response)).ShouldBe("org.parent_inactive");
        (await TreeAsync()).ShouldNotContain(u => u.GetProperty("code").GetString() == "UNDER");
    }

    [Fact]
    public async Task ORG_01_AC4_a_deactivated_unit_type_cannot_be_chosen_for_a_new_unit_and_existing_units_keep_it()
    {
        var zone = await CreateTypeAsync("ZONE");
        var south = await CreateUnitAsync(OrgRoot.UnitId, "SOUTH", zone);
        await DeactivateUnitAsync(south);
        (await PostTypeStatusAsync(zone, "deactivate")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var refused = await PostUnitAsync(OrgRoot.UnitId, "NORTH", zone);

        refused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ProblemCodeAsync(refused)).ShouldBe("org.unit_type_inactive");
        (await UnitAsync(south)).GetProperty("unitTypeId").GetGuid().ShouldBe(zone);
    }

    [Fact]
    public async Task ORG_01_AC6_a_re_activated_unit_type_can_be_chosen_for_new_units_again()
    {
        var zone = await CreateTypeAsync("ZONE");
        (await PostTypeStatusAsync(zone, "deactivate")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await PostUnitAsync(OrgRoot.UnitId, "NORTH", zone)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        (await PostTypeStatusAsync(zone, "activate")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await PostUnitAsync(OrgRoot.UnitId, "NORTH", zone)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ORG_03_AC1_ORG_03_AC3_a_unit_code_is_unique_ignoring_case_and_cannot_be_changed()
    {
        var zone = await CreateTypeAsync("ZONE");
        var south = await CreateUnitAsync(OrgRoot.UnitId, "south-1", zone);
        (await UnitAsync(south)).GetProperty("code").GetString().ShouldBe("SOUTH-1");

        var duplicate = await PostUnitAsync(OrgRoot.UnitId, "South-1", zone);
        duplicate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(duplicate)).ShouldBe("org.unit_code_taken");

        // The root's code is taken too, by the installation (C1).
        (await ProblemCodeAsync(await PostUnitAsync(OrgRoot.UnitId, "root", zone))).ShouldBe("org.unit_code_taken");

        // An attempt to send a new code with a rename is ignored: the contract has no such field (O9).
        var version = (await UnitAsync(south)).GetProperty("version").GetString();
        (await _admin.Client.PutAsJsonAsync(UnitUri(south), new { code = "OTHER", name = "South", version }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await UnitAsync(south)).GetProperty("code").GetString().ShouldBe("SOUTH-1");
    }

    [Theory]
    [InlineData("SOUTH ZONE")]
    [InlineData("")]
    [InlineData("SÜD")]
    [InlineData("A12345678901234567890123456789012345678901234567890")] // 51 characters
    public async Task ORG_03_AC3_a_unit_code_outside_the_format_is_rejected(string code)
    {
        var zone = await CreateTypeAsync("ZONE");

        var response = await PostUnitAsync(OrgRoot.UnitId, code, zone);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(response)).ShouldBe("org.code_invalid");
    }

    [Fact]
    public async Task ORG_03_AC3_a_duplicate_code_that_races_past_the_check_gets_the_same_error_from_the_database()
    {
        var zone = await CreateTypeAsync("ZONE");
        await using var first = app.Services.CreateAsyncScope();
        await using var second = app.Services.CreateAsyncScope();
        await AddUnitInScopeAsync(first.ServiceProvider, "SOUTH", zone);
        await AddUnitInScopeAsync(second.ServiceProvider, "south", zone);

        (await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct)).IsSuccess.ShouldBeTrue();
        var raced = await second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        raced.Error.ShouldBe(OrgErrors.UnitCodeTaken);
        (await app.WithDbContextAsync(context => context.OrgUnits.CountAsync(u => u.Code == OrgCode.Create("SOUTH").Value, Ct))).ShouldBe(1);
    }

    [Fact]
    public async Task ORG_03_AC2_the_head_must_be_an_active_user()
    {
        var zone = await CreateTypeAsync("ZONE");
        var south = await CreateUnitAsync(OrgRoot.UnitId, "SOUTH", zone);
        var gone = await app.CreateUserAsync("gone.head");
        (await _admin.Client.PostAsync(new Uri($"/api/v1/users/{gone.Id}/deactivate", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var inactive = await SetHeadAsync(south, gone.Id);
        inactive.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ProblemCodeAsync(inactive)).ShouldBe("org.head_inactive");

        var unknown = await SetHeadAsync(south, Guid.NewGuid());
        unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(unknown)).ShouldBe("org.head_unknown");

        (await UnitAsync(south)).GetProperty("headUserId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ORG_03_AC2_the_head_may_work_in_another_unit_and_head_several_units()
    {
        var zone = await CreateTypeAsync("ZONE");
        var south = await CreateUnitAsync(OrgRoot.UnitId, "SOUTH", zone);
        var north = await CreateUnitAsync(OrgRoot.UnitId, "NORTH", zone);
        var priya = await app.CreateUserAsync("priya.k"); // works in the root unit (O11)

        (await SetHeadAsync(south, priya.Id)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await SetHeadAsync(north, priya.Id)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        foreach (var unit in new[] { south, north })
        {
            var details = await UnitAsync(unit);
            details.GetProperty("headUserId").GetGuid().ShouldBe(priya.Id);
            details.GetProperty("headUsername").GetString().ShouldBe("priya.k");
        }
    }

    [Fact]
    public async Task ORG_03_AC4_the_admin_renames_a_unit_and_sets_changes_and_clears_its_head()
    {
        var zone = await CreateTypeAsync("ZONE");
        var south = await CreateUnitAsync(OrgRoot.UnitId, "SOUTH", zone);
        var priya = await app.CreateUserAsync("priya.s");
        var arun = await app.CreateUserAsync("arun.s");

        var version = (await UnitAsync(south)).GetProperty("version").GetString();
        (await _admin.Client.PutAsJsonAsync(UnitUri(south), new { name = "Southern Zone", version }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await SetHeadAsync(south, priya.Id)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await UnitAsync(south)).GetProperty("headUserId").GetGuid().ShouldBe(priya.Id);
        (await SetHeadAsync(south, arun.Id)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await UnitAsync(south)).GetProperty("headUserId").GetGuid().ShouldBe(arun.Id);
        (await SetHeadAsync(south, null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var details = await UnitAsync(south);
        details.GetProperty("name").GetString().ShouldBe("Southern Zone");
        details.GetProperty("code").GetString().ShouldBe("SOUTH");
        details.GetProperty("headUserId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Constitution_8_5_of_two_admins_renaming_the_same_unit_the_second_gets_a_conflict()
    {
        var zone = await CreateTypeAsync("ZONE");
        var south = await CreateUnitAsync(OrgRoot.UnitId, "SOUTH", zone);
        var version = (await UnitAsync(south)).GetProperty("version").GetString();

        (await _admin.Client.PutAsJsonAsync(UnitUri(south), new { name = "First", version }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var second = await _admin.Client.PutAsJsonAsync(UnitUri(south), new { name = "Second", version }, Ct);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(second)).ShouldBe("common.changed_by_someone_else");
        (await UnitAsync(south)).GetProperty("name").GetString().ShouldBe("First");
    }

    [Fact]
    public async Task A_unit_needs_a_known_parent_and_type_and_unknown_units_are_not_found()
    {
        var zone = await CreateTypeAsync("ZONE");

        (await ProblemCodeAsync(await PostUnitAsync(Guid.NewGuid(), "SOUTH", zone))).ShouldBe("org.parent_unknown");
        (await ProblemCodeAsync(await PostUnitAsync(OrgRoot.UnitId, "SOUTH", Guid.NewGuid()))).ShouldBe("org.unit_type_unknown");
        var missing = await _admin.Client.GetAsync(UnitUri(Guid.NewGuid()), Ct);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ProblemCodeAsync(missing)).ShouldBe("org.unit_not_found");
        (await _admin.Client.GetAsync(new Uri($"{UnitUri(Guid.NewGuid())}/users", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_users_of_a_unit_are_listed_by_page_with_a_total_and_the_details_count_the_active_ones()
    {
        var zone = await CreateTypeAsync("ZONE");
        var south = await CreateUnitAsync(OrgRoot.UnitId, "SOUTH", zone);
        foreach (var name in new[] { "unit.a", "unit.b", "unit.c" })
        {
            await PlaceUserAsync(await app.CreateUserAsync(name), south);
        }

        var inactive = await app.CreateUserAsync("unit.d");
        await PlaceUserAsync(inactive, south);
        (await _admin.Client.PostAsync(new Uri($"/api/v1/users/{inactive.Id}/deactivate", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var page = await _admin.Client.GetFromJsonAsync<JsonElement>(new Uri($"{UnitUri(south)}/users?page=1&size=2", UriKind.Relative), Ct);
        page.GetProperty("totalCount").GetInt32().ShouldBe(4);
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("username").GetString()).ShouldBe(["unit.a", "unit.b"]);
        (await UnitAsync(south)).GetProperty("activeUsers").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task Org_units_need_Platform_Admin_and_refuse_anonymous_callers()
    {
        var (_, designer) = await app.UserWithSessionAsync("units.designer", SystemRole.AppDesigner);
        using var _ = designer;
        using var anonymous = app.CreateClient();

        (await anonymous.GetAsync(new Uri("/api/v1/org-units/tree", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await designer.Client.GetAsync(new Uri("/api/v1/org-units/tree", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await designer.Client.GetAsync(UnitUri(OrgRoot.UnitId), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await designer.Client.PostAsJsonAsync(new Uri("/api/v1/org-units", UriKind.Relative), new { parentId = OrgRoot.UnitId, code = "X", name = "X", unitTypeId = OrgRoot.UnitTypeId }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await designer.Client.PutAsJsonAsync(new Uri($"{UnitUri(OrgRoot.UnitId)}/head", UriKind.Relative), new { headUserId = (Guid?)null, version = "AAAAAAAAAAA=" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static Uri UnitUri(Guid id) => new($"/api/v1/org-units/{id}", UriKind.Relative);

    private async Task<Guid> CreateTypeAsync(string code)
    {
        var response = await _admin.Client.PostAsJsonAsync(new Uri("/api/v1/unit-types", UriKind.Relative), new { code, name = $"{code} name" }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> PostTypeStatusAsync(Guid id, string action)
    {
        var list = await _admin.Client.GetFromJsonAsync<JsonElement>(new Uri("/api/v1/unit-types", UriKind.Relative), Ct);
        var version = list.EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == id).GetProperty("version").GetString();
        return await _admin.Client.PostAsJsonAsync(new Uri($"/api/v1/unit-types/{id}/{action}", UriKind.Relative), new { version }, Ct);
    }

    private Task<HttpResponseMessage> PostUnitAsync(Guid parentId, string code, Guid unitTypeId) =>
        _admin.Client.PostAsJsonAsync(new Uri("/api/v1/org-units", UriKind.Relative), new { parentId, code, name = $"{code} unit", unitTypeId }, Ct);

    private async Task<Guid> CreateUnitAsync(Guid parentId, string code, Guid unitTypeId)
    {
        var response = await PostUnitAsync(parentId, code, unitTypeId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldBe($"/api/v1/org-units/{id}");
        return id;
    }

    private async Task<List<JsonElement>> TreeAsync() =>
        [.. (await _admin.Client.GetFromJsonAsync<JsonElement>(new Uri("/api/v1/org-units/tree", UriKind.Relative), Ct)).EnumerateArray()];

    private Task<JsonElement> UnitAsync(Guid id) => _admin.Client.GetFromJsonAsync<JsonElement>(UnitUri(id), Ct);

    private async Task<HttpResponseMessage> SetHeadAsync(Guid unitId, Guid? headUserId)
    {
        var version = (await UnitAsync(unitId)).GetProperty("version").GetString();
        return await _admin.Client.PutAsJsonAsync(new Uri($"{UnitUri(unitId)}/head", UriKind.Relative), new { headUserId, version }, Ct);
    }

    private static async Task AddUnitInScopeAsync(IServiceProvider services, string code, Guid unitTypeId)
    {
        var units = services.GetRequiredService<IOrgUnitRepository>();
        var type = (await services.GetRequiredService<IUnitTypeRepository>().GetAsync(unitTypeId, Ct))!;
        var root = (await units.GetAsync(OrgRoot.UnitId, Ct))!;
        // Without touching the org structure, so only the code index can stop the second save.
        units.Add(OrgUnit.Create(Guid.NewGuid(), await services.GetRequiredService<IUnitNumberSource>().NextAsync(Ct), code, code, type, root).Value);
    }

    // Deactivating a unit comes with task T008; until then through the repositories.
    private async Task DeactivateUnitAsync(Guid unitId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var unit = (await scope.ServiceProvider.GetRequiredService<IOrgUnitRepository>().GetAsync(unitId, Ct))!;
        unit.Deactivate(activeChildUnits: 0, activeUsers: 0).IsSuccess.ShouldBeTrue();
        (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct)).IsSuccess.ShouldBeTrue();
    }

    // Users' org details come with task T010; until then through the repositories.
    private async Task PlaceUserAsync(User user, Guid unitId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var unit = (await scope.ServiceProvider.GetRequiredService<IOrgUnitRepository>().GetAsync(unitId, Ct))!;
        var loaded = (await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetAsync(user.Id, Ct))!;
        loaded.PlaceInUnit(unit).IsSuccess.ShouldBeTrue();
        (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct)).IsSuccess.ShouldBeTrue();
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
