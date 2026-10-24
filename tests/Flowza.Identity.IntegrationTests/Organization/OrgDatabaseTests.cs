using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Organization;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.Infrastructure.Persistence;
using Flowza.Identity.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Flowza.Identity.IntegrationTests.Organization;

/// <summary>
/// T005 / PA4, PA5: the organization's tables on <c>flowza_test_identity</c>: the installed root (C1), one root only, the
/// back-fill of users made with feature 001 (C2), the sequence, the repositories and the org-structure row version.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class OrgDatabaseTests(IdentityApp app) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await app.ResetDataAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task PA4_the_migration_creates_the_org_tables_and_the_unit_number_sequence()
    {
        var tables = await app.WithDbContextAsync(context => context.Database
            .SqlQueryRaw<string>("SELECT TABLE_NAME AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo'")
            .ToListAsync(Ct));
        foreach (var table in new[] { "UnitTypes", "OrgUnits", "BusinessRoles", "UserBusinessRoles", "OrgStructure" })
        {
            tables.ShouldContain(table);
        }

        var sequenceStart = await app.WithDbContextAsync(context => context.Database
            .SqlQueryRaw<long>("SELECT CAST(start_value AS bigint) AS Value FROM sys.sequences WHERE name = 'UnitNumbers'")
            .ToListAsync(Ct));
        sequenceStart.ShouldBe([2L]);
    }

    [Fact]
    public async Task ORG_02_AC7_a_new_installation_has_exactly_one_root_unit_and_one_unit_type_with_the_fixed_codes()
    {
        // The org tables as the migration itself creates them, not as the test reset restores them.
        await ReapplyOrgHierarchyMigrationAsync();

        var (types, units, structure) = await app.WithDbContextAsync(async context => (
            await context.UnitTypes.AsNoTracking().ToListAsync(Ct),
            await context.OrgUnits.AsNoTracking().ToListAsync(Ct),
            await context.OrgStructure.AsNoTracking().ToListAsync(Ct)));

        var type = types.ShouldHaveSingleItem();
        type.Id.ShouldBe(OrgRoot.UnitTypeId);
        type.Code.Value.ShouldBe("ROOT");
        type.Name.ShouldBe("Organization");
        type.IsActive.ShouldBeTrue();

        var root = units.ShouldHaveSingleItem();
        root.Id.ShouldBe(OrgRoot.UnitId);
        root.Code.Value.ShouldBe("ROOT");
        root.Name.ShouldBe("Organization");
        root.UnitTypeId.ShouldBe(OrgRoot.UnitTypeId);
        root.IsRoot.ShouldBeTrue();
        root.Number.ShouldBe(1);
        root.Path.Value.ShouldBe("/1/");
        root.HeadUserId.ShouldBeNull();
        root.IsActive.ShouldBeTrue();

        structure.ShouldHaveSingleItem().Id.ShouldBe(OrgStructure.SingletonId);
    }

    [Fact]
    public async Task ORG_02_AC7_the_installed_root_and_its_type_can_be_renamed_and_keep_their_codes()
    {
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var type = (await services.GetRequiredService<IUnitTypeRepository>().GetAsync(OrgRoot.UnitTypeId, Ct))!;
            var root = (await services.GetRequiredService<IOrgUnitRepository>().GetAsync(OrgRoot.UnitId, Ct))!;
            type.Rename("Head Office").IsSuccess.ShouldBeTrue();
            root.Rename("Head Office").IsSuccess.ShouldBeTrue();
            (await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct)).IsSuccess.ShouldBeTrue();
        }

        var (savedType, savedRoot) = await app.WithDbContextAsync(async context => (
            await context.UnitTypes.AsNoTracking().SingleAsync(t => t.Id == OrgRoot.UnitTypeId, Ct),
            await context.OrgUnits.AsNoTracking().SingleAsync(u => u.Id == OrgRoot.UnitId, Ct)));
        (savedType.Code.Value, savedType.Name).ShouldBe(("ROOT", "Head Office"));
        (savedRoot.Code.Value, savedRoot.Name).ShouldBe(("ROOT", "Head Office"));
    }

    [Fact]
    public async Task ORG_02_AC5_the_database_refuses_a_second_root_unit()
    {
        var secondRoot = OrgUnit.CreateRoot(Guid.NewGuid(), 999_999, OrgCode.Create("ROOT2").Value, "Second root", OrgRoot.UnitTypeId);

        var error = await Should.ThrowAsync<DbUpdateException>(() => app.WithDbContextAsync(context =>
        {
            context.OrgUnits.Add(secondRoot);
            return context.SaveChangesAsync(Ct);
        }));

        var sql = error.InnerException.ShouldBeOfType<SqlException>();
        sql.Number.ShouldBe(2601); // duplicate key in a unique index
        sql.Message.ShouldContain("IX_OrgUnits_OneRoot");
        (await app.WithDbContextAsync(context => context.OrgUnits.CountAsync(u => u.ParentId == null, Ct))).ShouldBe(1);
    }

    [Fact]
    public async Task ORG_04_AC1_the_migration_places_every_user_made_before_002_in_the_root_unit()
    {
        var active = await app.CreateUserAsync("before.org.a");
        var inactive = await app.CreateUserAsync("before.org.b");
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            (await users.GetAsync(inactive.Id, Ct))!.Deactivate(activePlatformAdminCount: 0).IsSuccess.ShouldBeTrue();
            (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct)).IsSuccess.ShouldBeTrue();
        }

        // Back to the 001 tables (no primary unit column), then the OrgHierarchy migration as on an upgraded installation.
        await ReapplyOrgHierarchyMigrationAsync();

        var placed = await app.WithDbContextAsync(context => context.Users.AsNoTracking()
            .Select(u => new { u.Id, u.PrimaryUnitId, u.ManagerId })
            .ToListAsync(Ct));
        placed.Select(u => u.Id).ShouldBe([active.Id, inactive.Id], ignoreOrder: true);
        placed.ShouldAllBe(u => u.PrimaryUnitId == OrgRoot.UnitId && u.ManagerId == null);
    }

    [Fact]
    public async Task T005_units_roles_and_a_users_place_round_trip_and_a_units_descendants_are_found_by_its_path()
    {
        var zoneType = UnitType.Create(Guid.NewGuid(), "ZONE", "Zone").Value;
        var role = BusinessRole.Create(Guid.NewGuid(), "credit_officer", "Credit Officer").Value;
        var manager = await app.CreateUserAsync("org.manager");
        var member = await app.CreateUserAsync("org.member");
        OrgUnit south = null!, chennai = null!, branch = null!, north = null!;

        // Numbers 4 and 42 on purpose: "/1/4/" must not find "/1/42/" (plan §8).
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var units = services.GetRequiredService<IOrgUnitRepository>();
            var root = (await units.GetAsync(OrgRoot.UnitId, Ct))!;
            south = OrgUnit.Create(Guid.NewGuid(), 4, "SOUTH", "South Zone", zoneType, root).Value;
            north = OrgUnit.Create(Guid.NewGuid(), 42, "NORTH", "North Zone", zoneType, root).Value;
            chennai = OrgUnit.Create(Guid.NewGuid(), 9, "CHENNAI", "Chennai Region", zoneType, south).Value;
            branch = OrgUnit.Create(Guid.NewGuid(), 13, "TNAGAR", "T Nagar Branch", zoneType, chennai).Value;
            var northChild = OrgUnit.Create(Guid.NewGuid(), 43, "NORTH-1", "North Region", zoneType, north).Value;

            services.GetRequiredService<IUnitTypeRepository>().Add(zoneType);
            services.GetRequiredService<IBusinessRoleRepository>().Add(role);
            foreach (var unit in new[] { south, north, chennai, branch, northChild })
            {
                units.Add(unit);
            }

            var users = services.GetRequiredService<IUserRepository>();
            var loadedManager = (await users.GetAsync(manager.Id, Ct))!;
            var loadedMember = (await users.GetAsync(member.Id, Ct))!;
            south.SetHead(loadedManager).IsSuccess.ShouldBeTrue();
            loadedMember.PlaceInUnit(branch).IsSuccess.ShouldBeTrue();
            loadedMember.SetManager(loadedManager, ManagerChain.Walk(loadedManager.Id, _ => null)).IsSuccess.ShouldBeTrue();
            loadedMember.AddBusinessRole(role).IsSuccess.ShouldBeTrue();
            (await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct)).IsSuccess.ShouldBeTrue();
        }

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var units = services.GetRequiredService<IOrgUnitRepository>();
            var loadedSouth = (await units.GetAsync(south.Id, Ct))!;
            loadedSouth.Path.Value.ShouldBe("/1/4/");
            loadedSouth.HeadUserId.ShouldBe(manager.Id);
            loadedSouth.Code.Value.ShouldBe("SOUTH");

            var below = await units.GetDescendantsAsync(loadedSouth, Ct);
            below.Select(u => u.Id).ShouldBe([chennai.Id, branch.Id], ignoreOrder: true);
            (await units.GetDescendantsAsync((await units.GetAsync(branch.Id, Ct))!, Ct)).ShouldBeEmpty();

            (await units.IsCodeTakenAsync(OrgCode.Create("south").Value, Ct)).ShouldBeTrue();
            (await units.IsCodeTakenAsync(OrgCode.Create("WEST").Value, Ct)).ShouldBeFalse();
            (await services.GetRequiredService<IUnitTypeRepository>().IsCodeTakenAsync(OrgCode.Create("zone").Value, Ct)).ShouldBeTrue();
            (await services.GetRequiredService<IBusinessRoleRepository>().IsCodeTakenAsync(OrgCode.Create("CREDIT_OFFICER").Value, Ct)).ShouldBeTrue();

            var loadedMember = (await services.GetRequiredService<IUserRepository>().GetAsync(member.Id, Ct))!;
            loadedMember.PrimaryUnitId.ShouldBe(branch.Id);
            loadedMember.ManagerId.ShouldBe(manager.Id);
            loadedMember.BusinessRoleIds.ShouldBe([role.Id]);

            // "Remove member" removes the membership row (plan §5.2).
            loadedMember.RemoveBusinessRole(role.Id).IsSuccess.ShouldBeTrue();
            (await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct)).IsSuccess.ShouldBeTrue();
        }

        (await app.WithDbContextAsync(context => context.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM UserBusinessRoles").ToListAsync(Ct))).ShouldBe([0]);
    }

    [Fact]
    public async Task T005_unit_numbers_come_from_the_sequence_above_the_roots_number()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var numbers = scope.ServiceProvider.GetRequiredService<IUnitNumberSource>();

        var first = await numbers.NextAsync(Ct);
        var second = await numbers.NextAsync(Ct);

        first.ShouldBeGreaterThan(OrgRoot.UnitNumber);
        second.ShouldBeGreaterThan(first);
    }

    [Fact]
    public async Task T005_of_two_structural_changes_at_the_same_moment_only_the_first_is_saved()
    {
        await using var first = app.Services.CreateAsyncScope();
        await using var second = app.Services.CreateAsyncScope();
        var firstStructure = await first.ServiceProvider.GetRequiredService<IOrgStructureRepository>().GetAsync(Ct);
        var secondStructure = await second.ServiceProvider.GetRequiredService<IOrgStructureRepository>().GetAsync(Ct);

        firstStructure.Touch(DateTimeOffset.UtcNow);
        secondStructure.Touch(DateTimeOffset.UtcNow);
        var firstSave = await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        var secondSave = await second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        firstSave.IsSuccess.ShouldBeTrue();
        secondSave.Error.ShouldBe(UnitOfWork.ConcurrencyConflict);
        (await app.WithDbContextAsync(context => context.OrgStructure.AsNoTracking().SingleAsync(Ct))).ChangeCount.ShouldBe(1);
    }

    /// <summary>
    /// Takes the database back to the migration before OrgHierarchy (the tables of feature 001, users kept) and applies
    /// OrgHierarchy again, as on an installation upgraded from 001.
    /// </summary>
    private Task<bool> ReapplyOrgHierarchyMigrationAsync() => app.WithDbContextAsync(async context =>
    {
        var migrations = context.Database.GetMigrations().ToList();
        var orgHierarchy = migrations.FindIndex(m => m.EndsWith("_OrgHierarchy", StringComparison.Ordinal));
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[orgHierarchy - 1], Ct);
        await migrator.MigrateAsync(cancellationToken: Ct);
        return true;
    });
}
