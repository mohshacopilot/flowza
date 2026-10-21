using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Infrastructure;
using Flowza.Identity.Infrastructure.AspNetIdentity;
using Flowza.Identity.Infrastructure.Passwords;
using Flowza.Identity.Infrastructure.Persistence;
using Flowza.Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowza.Identity.IntegrationTests.Accounts;

/// <summary>T012: password rules (IAM-06) and lockout (IAM-07) on the real service and database.</summary>
[Collection(IdentityTestGroup.Name)]
public sealed class PasswordAndLockoutTests(IdentityApp app) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await app.ResetDataAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public void PA7_the_embedded_common_password_list_has_ten_thousand_entries()
    {
        EmbeddedCommonPasswordList.Count.ShouldBeInRange(9_900, 10_100);
    }

    [Theory]
    [InlineData("short-pw", "passwords.too_short")]
    [InlineData("1234567890", "passwords.common")]
    [InlineData("Qwertyuiop", "passwords.common")]
    [InlineData("meena.r.2026", "passwords.same_as_username")]
    public async Task IAM_06_AC1_weak_passwords_are_rejected_with_a_clear_reason(string password, string expectedCode)
    {
        var user = await app.CreateUserAsync("meena.r.2026", password: null);

        var result = await app.InScopeAsync(async services =>
        {
            var loaded = await services.GetRequiredService<IUserRepository>().GetAsync(user.Id, Ct);
            return await services.GetRequiredService<IUserCredentials>().SetPasswordAsync(loaded!, password, Ct);
        });

        result.Error!.FieldErrors!["newPassword"].ShouldContain(expectedCode);
    }

    [Fact]
    public async Task IAM_06_AC1_a_strong_password_is_stored_only_as_a_hash()
    {
        var user = await app.CreateUserAsync("ravi.k");

        var hash = await app.WithDbContextAsync(context => context.Users
            .Where(u => u.Id == user.Id)
            .Select(u => EF.Property<string>(u, UserCredentialColumns.PasswordHash))
            .SingleAsync(Ct));

        hash.ShouldNotBeNullOrEmpty();
        hash.ShouldNotContain(IdentityAppUsers.Password);
        (await LoginAsync("ravi.k", IdentityAppUsers.Password)).Outcome.ShouldBe(LoginOutcome.Succeeded);
    }

    [Fact]
    public async Task IAM_07_AC1_five_wrong_passwords_lock_the_account_for_15_minutes()
    {
        await app.CreateUserAsync("priya.s");

        for (var attempt = 1; attempt < IdentityInfrastructureExtensions.LockoutAttempts; attempt++)
        {
            (await LoginAsync("priya.s", "wrong-password-1")).Outcome.ShouldBe(LoginOutcome.InvalidCredentials);
        }

        var before = DateTimeOffset.UtcNow;
        (await LoginAsync("priya.s", "wrong-password-1")).Outcome.ShouldBe(LoginOutcome.LockedOut);

        // Locked: even the right password is refused.
        (await LoginAsync("priya.s", IdentityAppUsers.Password)).Outcome.ShouldBe(LoginOutcome.LockedOut);

        var lockoutEnd = await LockoutEndAsync("priya.s");
        lockoutEnd!.Value.ShouldBeInRange(before.AddMinutes(15).AddSeconds(-5), DateTimeOffset.UtcNow.AddMinutes(15).AddSeconds(5));

        // 15 minutes later (ASP.NET Core Identity reads the system clock, so the stored end is moved instead).
        await SetLockoutEndAsync("priya.s", DateTimeOffset.UtcNow.AddSeconds(-1));
        (await LoginAsync("priya.s", IdentityAppUsers.Password)).Outcome.ShouldBe(LoginOutcome.Succeeded);
    }

    [Fact]
    public async Task IAM_07_AC1_the_admin_can_unlock_a_locked_account_at_once()
    {
        var user = await app.CreateUserAsync("meena.r");
        for (var attempt = 0; attempt < IdentityInfrastructureExtensions.LockoutAttempts; attempt++)
        {
            await LoginAsync("meena.r", "wrong-password-1");
        }

        await app.InScopeAsync(async services =>
        {
            var loaded = await services.GetRequiredService<IUserRepository>().GetAsync(user.Id, Ct);
            var credentials = services.GetRequiredService<IUserCredentials>();
            (await credentials.IsLockedOutAsync(loaded!, Ct)).ShouldBeTrue();
            await credentials.UnlockAsync(loaded!, Ct);
            return await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });

        (await LoginAsync("meena.r", IdentityAppUsers.Password)).Outcome.ShouldBe(LoginOutcome.Succeeded);
    }

    [Fact]
    public async Task A_right_password_resets_the_count_of_wrong_ones()
    {
        await app.CreateUserAsync("asha.n");
        for (var attempt = 1; attempt < IdentityInfrastructureExtensions.LockoutAttempts; attempt++)
        {
            await LoginAsync("asha.n", "wrong-password-1");
        }

        (await LoginAsync("asha.n", IdentityAppUsers.Password)).Outcome.ShouldBe(LoginOutcome.Succeeded);
        (await LoginAsync("asha.n", "wrong-password-1")).Outcome.ShouldBe(LoginOutcome.InvalidCredentials);
    }

    [Fact]
    public async Task IAM_01_AC2_unknown_users_and_inactive_users_get_the_same_answer_as_a_wrong_password()
    {
        var user = await app.CreateUserAsync("gone.user");
        await app.CreateUserAsync("other.admin", roles: Flowza.Identity.Domain.Users.SystemRole.PlatformAdmin);
        await app.InScopeAsync(async services =>
        {
            var loaded = await services.GetRequiredService<IUserRepository>().GetAsync(user.Id, Ct);
            loaded!.Deactivate(activePlatformAdminCount: 1);
            return await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });

        (await LoginAsync("nobody.here", IdentityAppUsers.Password)).Outcome.ShouldBe(LoginOutcome.InvalidCredentials);
        (await LoginAsync("gone.user", IdentityAppUsers.Password)).Outcome.ShouldBe(LoginOutcome.InvalidCredentials);
        (await LoginAsync("other.admin", "wrong-password-1")).Outcome.ShouldBe(LoginOutcome.InvalidCredentials);
    }

    private Task<LoginCheck> LoginAsync(string username, string password) =>
        app.InScopeAsync(services => services.GetRequiredService<PasswordLogin>().CheckAsync(username, password, Ct));

    private Task<DateTimeOffset?> LockoutEndAsync(string username)
    {
        var normalized = Flowza.Identity.Domain.Users.Username.Normalize(username);
        return app.WithDbContextAsync(context => context.Users
            .Where(u => u.NormalizedUsername == normalized)
            .Select(u => EF.Property<DateTimeOffset?>(u, UserCredentialColumns.LockoutEnd))
            .SingleAsync(Ct));
    }

    private Task<int> SetLockoutEndAsync(string username, DateTimeOffset end)
    {
        var normalized = Flowza.Identity.Domain.Users.Username.Normalize(username);
        return app.WithDbContextAsync(context => context.Users
            .Where(u => u.NormalizedUsername == normalized)
            .ExecuteUpdateAsync(set => set.SetProperty(u => EF.Property<DateTimeOffset?>(u, UserCredentialColumns.LockoutEnd), end), Ct));
    }
}
