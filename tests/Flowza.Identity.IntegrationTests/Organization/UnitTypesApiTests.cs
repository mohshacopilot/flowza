using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Organization;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.Infrastructure.Persistence;
using Flowza.Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowza.Identity.IntegrationTests.Organization;

/// <summary>T006 / ORG-01: the unit types API for Platform Admins (plan 002 §5.4), on the real service and database.</summary>
[Collection(IdentityTestGroup.Name)]
public sealed class UnitTypesApiTests(IdentityApp app) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ApiSession _admin = null!;

    public async ValueTask InitializeAsync()
    {
        await app.ResetDataAsync();
        (_, _admin) = await app.UserWithSessionAsync("types.admin", SystemRole.PlatformAdmin);
    }

    public ValueTask DisposeAsync()
    {
        _admin.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ORG_01_AC1_the_admin_creates_a_unit_type_and_it_is_listed_active_without_units()
    {
        var created = await _admin.Client.PostAsJsonAsync(UnitTypes(), new { code = "ZONE", name = "Zone" }, Ct);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        created.Headers.Location!.ToString().ShouldBe($"/api/v1/unit-types/{id}");

        var listed = await ItemAsync(id);
        listed.GetProperty("code").GetString().ShouldBe("ZONE");
        listed.GetProperty("name").GetString().ShouldBe("Zone");
        listed.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        listed.GetProperty("activeUnits").GetInt32().ShouldBe(0);
        listed.GetProperty("version").GetString().ShouldNotBeNullOrEmpty();

        // The installed root type is listed too, with the root unit (C1).
        var root = await ItemAsync(OrgRoot.UnitTypeId);
        root.GetProperty("code").GetString().ShouldBe("ROOT");
        root.GetProperty("activeUnits").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task ORG_01_AC5_a_code_in_lower_case_is_stored_in_upper_case_and_clashes_with_the_same_code_in_another_case()
    {
        var id = await CreateAsync("branch-1", "Branch");
        (await ItemAsync(id)).GetProperty("code").GetString().ShouldBe("BRANCH-1");

        var duplicate = await _admin.Client.PostAsJsonAsync(UnitTypes(), new { code = "Branch-1", name = "Another" }, Ct);

        duplicate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await duplicate.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe("org.unit_type_code_taken");
        problem.GetProperty("errors").GetProperty("code").EnumerateArray().Single().GetString().ShouldBe("org.unit_type_code_taken");
        (await ListAsync()).Count(t => t.GetProperty("code").GetString() == "BRANCH-1").ShouldBe(1);
    }

    [Theory]
    [InlineData("ZONE 1")]
    [InlineData("")]
    [InlineData("ZÖNE")]
    [InlineData("A12345678901234567890123456789012345678901234567890")] // 51 characters
    public async Task ORG_01_AC5_a_code_outside_the_format_is_rejected(string code)
    {
        var response = await _admin.Client.PostAsJsonAsync(UnitTypes(), new { code, name = "Zone" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(response)).ShouldBe("org.code_invalid");
    }

    [Fact]
    public async Task ORG_01_AC1_a_name_is_required()
    {
        var response = await _admin.Client.PostAsJsonAsync(UnitTypes(), new { code = "ZONE", name = "  " }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(response)).ShouldBe("org.name_invalid");
    }

    [Fact]
    public async Task ORG_01_AC1_a_duplicate_that_races_past_the_check_gets_the_same_error_from_the_database()
    {
        // Two admins create the same code at the same moment: both checks pass, the unique index stops the second save.
        await using var first = app.Services.CreateAsyncScope();
        await using var second = app.Services.CreateAsyncScope();
        first.ServiceProvider.GetRequiredService<IUnitTypeRepository>().Add(UnitType.Create(Guid.NewGuid(), "REGION", "Region").Value);
        second.ServiceProvider.GetRequiredService<IUnitTypeRepository>().Add(UnitType.Create(Guid.NewGuid(), "region", "Region").Value);

        (await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct)).IsSuccess.ShouldBeTrue();
        var raced = await second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        raced.Error.ShouldBe(OrgErrors.UnitTypeCodeTaken);
    }

    [Fact]
    public async Task ORG_01_AC2_renaming_changes_the_name_and_the_code_stays()
    {
        var id = await CreateAsync("DEPT", "Department");
        var version = (await ItemAsync(id)).GetProperty("version").GetString();

        // An attempt to send a new code is ignored: the contract has no such field (O9).
        var renamed = await _admin.Client.PutAsJsonAsync(UnitTypeUri(id), new { code = "TEAM", name = "Team", version }, Ct);

        renamed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var listed = await ItemAsync(id);
        listed.GetProperty("code").GetString().ShouldBe("DEPT");
        listed.GetProperty("name").GetString().ShouldBe("Team");
    }

    [Fact]
    public async Task ORG_01_AC3_a_unit_type_used_by_active_units_cannot_be_deactivated()
    {
        var id = await CreateAsync("ZONE", "Zone");
        await AddUnitAsync(id, "SOUTH");

        var refused = await PostStatusAsync(id, "deactivate");

        refused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ProblemCodeAsync(refused)).ShouldBe("org.unit_type_in_use");
        var listed = await ItemAsync(id);
        listed.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        listed.GetProperty("activeUnits").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task ORG_01_AC4_once_its_units_are_deactivated_the_type_can_be_deactivated_and_existing_units_keep_it()
    {
        var id = await CreateAsync("ZONE", "Zone");
        var unitId = await AddUnitAsync(id, "SOUTH");
        await DeactivateUnitAsync(unitId);

        (await PostStatusAsync(id, "deactivate")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await ItemAsync(id)).GetProperty("isActive").GetBoolean().ShouldBeFalse();
        var unit = await app.WithDbContextAsync(context => context.OrgUnits.AsNoTracking().SingleAsync(u => u.Id == unitId, Ct));
        unit.UnitTypeId.ShouldBe(id);

        // P9 is a rule over several records: the deactivation touched the org structure (plan §5.1).
        (await app.WithDbContextAsync(context => context.OrgStructure.AsNoTracking().SingleAsync(Ct))).ChangeCount.ShouldBe(1);
    }

    [Fact]
    public async Task ORG_01_AC6_a_deactivated_unit_type_can_be_activated_again()
    {
        var id = await CreateAsync("ZONE", "Zone");
        (await PostStatusAsync(id, "deactivate")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ItemAsync(id)).GetProperty("isActive").GetBoolean().ShouldBeFalse();

        (await PostStatusAsync(id, "activate")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await ItemAsync(id)).GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Constitution_8_5_a_change_with_an_old_version_is_a_conflict_and_a_missing_version_is_refused()
    {
        var id = await CreateAsync("ZONE", "Zone");
        var version = (await ItemAsync(id)).GetProperty("version").GetString();
        (await _admin.Client.PutAsJsonAsync(UnitTypeUri(id), new { name = "First", version }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var stale = await _admin.Client.PutAsJsonAsync(UnitTypeUri(id), new { name = "Second", version }, Ct);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(stale)).ShouldBe("common.changed_by_someone_else");

        var staleStatus = await _admin.Client.PostAsJsonAsync(new Uri($"{UnitTypeUri(id)}/deactivate", UriKind.Relative), new { version }, Ct);
        staleStatus.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var missing = await _admin.Client.PutAsJsonAsync(UnitTypeUri(id), new { name = "Third" }, Ct);
        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(missing)).ShouldBe("common.version_required");

        var listed = await ItemAsync(id);
        listed.GetProperty("name").GetString().ShouldBe("First");
        listed.GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task ORG_01_unit_types_need_Platform_Admin_and_refuse_anonymous_callers()
    {
        var (_, designer) = await app.UserWithSessionAsync("types.designer", SystemRole.AppDesigner);
        using var _ = designer;
        using var anonymous = app.CreateClient();

        (await anonymous.GetAsync(UnitTypes(), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await designer.Client.GetAsync(UnitTypes(), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await designer.Client.PostAsJsonAsync(UnitTypes(), new { code = "ZONE", name = "Zone" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await designer.Client.PostAsJsonAsync(new Uri($"{UnitTypeUri(OrgRoot.UnitTypeId)}/deactivate", UriKind.Relative), new { version = "AAAAAAAAAAA=" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_unknown_unit_type_is_not_found()
    {
        var response = await _admin.Client.PutAsJsonAsync(UnitTypeUri(Guid.NewGuid()), new { name = "Zone", version = "AAAAAAAAAAA=" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ProblemCodeAsync(response)).ShouldBe("org.unit_type_not_found");
    }

    private static Uri UnitTypes() => new("/api/v1/unit-types", UriKind.Relative);

    private static Uri UnitTypeUri(Guid id) => new($"/api/v1/unit-types/{id}", UriKind.Relative);

    private async Task<Guid> CreateAsync(string code, string name)
    {
        var response = await _admin.Client.PostAsJsonAsync(UnitTypes(), new { code, name }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task<List<JsonElement>> ListAsync() =>
        [.. (await _admin.Client.GetFromJsonAsync<JsonElement>(UnitTypes(), Ct)).EnumerateArray()];

    private async Task<JsonElement> ItemAsync(Guid id) => (await ListAsync()).Single(t => t.GetProperty("id").GetGuid() == id);

    private async Task<HttpResponseMessage> PostStatusAsync(Guid id, string action)
    {
        var version = (await ItemAsync(id)).GetProperty("version").GetString();
        return await _admin.Client.PostAsJsonAsync(new Uri($"{UnitTypeUri(id)}/{action}", UriKind.Relative), new { version }, Ct);
    }

    // The units API comes with task T007; until then a unit is added through the repositories under the root.
    private async Task<Guid> AddUnitAsync(Guid unitTypeId, string code)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var units = services.GetRequiredService<IOrgUnitRepository>();
        var type = (await services.GetRequiredService<IUnitTypeRepository>().GetAsync(unitTypeId, Ct))!;
        var root = (await units.GetAsync(OrgRoot.UnitId, Ct))!;
        var unit = OrgUnit.Create(Guid.NewGuid(), await services.GetRequiredService<IUnitNumberSource>().NextAsync(Ct), code, code, type, root).Value;
        units.Add(unit);
        (await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct)).IsSuccess.ShouldBeTrue();
        return unit.Id;
    }

    private async Task DeactivateUnitAsync(Guid unitId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var unit = (await scope.ServiceProvider.GetRequiredService<IOrgUnitRepository>().GetAsync(unitId, Ct))!;
        unit.Deactivate(activeChildUnits: 0, activeUsers: 0).IsSuccess.ShouldBeTrue();
        (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct)).IsSuccess.ShouldBeTrue();
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
