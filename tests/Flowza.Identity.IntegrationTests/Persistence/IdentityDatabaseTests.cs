using Flowza.Identity.Domain.Users;
using Flowza.Identity.IntegrationTests.Infrastructure;
using Flowza.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Identity.IntegrationTests.Persistence;

/// <summary>T011 / PA3: the Identity migration creates the approved tables on <c>flowza_test_identity</c>.</summary>
[Collection(IdentityTestGroup.Name)]
public sealed class IdentityDatabaseTests(IdentityApp app)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PA3_the_migration_creates_the_identity_tables()
    {
        var tables = await app.WithDbContextAsync(context => context.Database
            .SqlQueryRaw<string>("SELECT TABLE_SCHEMA + '.' + TABLE_NAME AS Value FROM INFORMATION_SCHEMA.TABLES")
            .ToListAsync(Ct));

        string[] expected =
        [
            "dbo.Users", "dbo.Roles", "dbo.UserRoles", "dbo.PasswordHistory", "dbo.DataProtectionKeys",
            "dbo.OpenIddictApplications", "dbo.OpenIddictAuthorizations", "dbo.OpenIddictScopes", "dbo.OpenIddictTokens",
        ];
        foreach (var table in expected)
        {
            tables.ShouldContain(table);
        }

        tables.ShouldContain(t => t.StartsWith("wolverine.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PA3_the_three_system_roles_are_seeded_with_fixed_ids()
    {
        var roles = await app.WithDbContextAsync(context => context.Roles.AsNoTracking().ToListAsync(Ct));

        roles.Select(r => (r.Id, r.Name)).ShouldBe(SystemRole.All.Select(r => (r.Id, r.Name)), ignoreOrder: true);
    }

    [Fact]
    public async Task A_user_with_roles_round_trips_and_username_and_email_are_unique_ignoring_case()
    {
        await app.ResetDataAsync();
        var user = User.Create(Guid.NewGuid(), "ravi.k", "Ravi Kumar", "ravi@bank.in", null, [SystemRole.AppDesigner]).Value;
        await app.WithDbContextAsync(async context =>
        {
            context.Users.Add(user);
            return await context.SaveChangesAsync(Ct);
        });

        var loaded = await app.WithDbContextAsync(context => context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id, Ct));
        loaded.Username.Value.ShouldBe("ravi.k");
        loaded.Email.Value.ShouldBe("ravi@bank.in");
        loaded.Roles.ShouldBe([SystemRole.AppDesigner, SystemRole.User], ignoreOrder: true);

        var sameNameOtherCase = User.Create(Guid.NewGuid(), "RAVI.K", "Other", "other@bank.in", null, []).Value;
        await Should.ThrowAsync<DbUpdateException>(() => app.WithDbContextAsync(async context =>
        {
            context.Users.Add(sameNameOtherCase);
            return await context.SaveChangesAsync(Ct);
        }));

        var sameEmailOtherCase = User.Create(Guid.NewGuid(), "other", "Other", "RAVI@BANK.IN", null, []).Value;
        await Should.ThrowAsync<DbUpdateException>(() => app.WithDbContextAsync(async context =>
        {
            context.Users.Add(sameEmailOtherCase);
            return await context.SaveChangesAsync(Ct);
        }));
    }
}
