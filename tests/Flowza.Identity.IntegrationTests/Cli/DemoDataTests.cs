using System.Net;
using Flowza.Cli;
using Flowza.Cli.DemoData;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.IntegrationTests.Connect;
using Flowza.Identity.IntegrationTests.Infrastructure;
using Flowza.Testing;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Identity.IntegrationTests.Cli;

/// <summary>T028 / PLT-07 (C5): <c>flowza demo-data</c> on <c>flowza_test_identity</c>.</summary>
[Collection(IdentityTestGroup.Name)]
public sealed class DemoDataTests(IdentityApp app) : IAsyncLifetime
{
    private static readonly Dictionary<string, string?> s_settings = new()
    {
        ["ConnectionStrings:identity-db"] = TestSqlServer.ConnectionString(IdentityApp.Database),
        ["ConnectionStrings:messaging-db"] = TestSqlServer.ConnectionString("flowza_test_messaging"),
        ["Messaging:Transport"] = "SqlServer",
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await app.ResetDataAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task PLT_07_AC1_demo_users_load_into_an_empty_database_with_their_roles()
    {
        var (exitCode, output, error) = await RunAsync("demo-data");

        exitCode.ShouldBe(FlowzaCli.Ok, error);
        output.ShouldContain($"{DemoUsers.All.Count} demo users loaded");
        output.ShouldContain(DemoUsers.Password, customMessage: "the documented demo password is shown (C5)");

        var users = await app.WithDbContextAsync(context => context.Users.AsNoTracking().ToListAsync(Ct));
        users.Select(u => u.Username.Value).ShouldBe(DemoUsers.All.Select(u => u.Username), ignoreOrder: true);
        users.ShouldAllBe(u => u.IsActive && !u.MustChangePassword);
        users.ShouldAllBe(u => u.Roles.Contains(SystemRole.User));
        users.Single(u => u.Username.Value == "demo.admin").Roles.ShouldContain(SystemRole.PlatformAdmin);
        users.Single(u => u.Username.Value == "demo.designer").Roles.ShouldContain(SystemRole.AppDesigner);
    }

    [Fact]
    public async Task PLT_07_C5_a_demo_user_logs_in_with_the_demo_password_without_a_forced_change()
    {
        (await RunAsync("demo-data")).ExitCode.ShouldBe(FlowzaCli.Ok);

        using var browser = app.CreateBrowser();
        var login = await LoginBrowser.PostLoginAsync(browser, "demo.admin", DemoUsers.Password, "/", Ct);

        login.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        login.Headers.Location!.ToString().ShouldNotStartWith("/account/change-password");
        login.Headers.Location!.ToString().ShouldNotStartWith("/account/login");
    }

    [Fact]
    public async Task PLT_07_AC2_demo_data_refuses_when_the_database_has_a_user_and_changes_nothing()
    {
        (await RunAsync("init-admin", "--email", "admin@bank.example")).ExitCode.ShouldBe(FlowzaCli.Ok);

        var (exitCode, output, error) = await RunAsync("demo-data");

        exitCode.ShouldBe(FlowzaCli.Refused);
        error.ShouldContain("identity database already has data");
        output.ShouldNotContain(DemoUsers.Password);
        var usernames = await app.WithDbContextAsync(context => context.Users.Select(u => u.Username).ToListAsync(Ct));
        usernames.ShouldHaveSingleItem().Value.ShouldBe("admin");
    }

    [Fact]
    public async Task PLT_07_AC2_a_second_run_refuses()
    {
        (await RunAsync("demo-data")).ExitCode.ShouldBe(FlowzaCli.Ok);

        var (exitCode, _, _) = await RunAsync("demo-data");

        exitCode.ShouldBe(FlowzaCli.Refused);
        (await app.WithDbContextAsync(context => context.Users.CountAsync(Ct))).ShouldBe(DemoUsers.All.Count);
    }

    [Fact]
    public async Task PLT_07_AC2_an_inactive_user_still_counts_as_data()
    {
        (await RunAsync("init-admin", "--email", "admin@bank.example")).ExitCode.ShouldBe(FlowzaCli.Ok);
        await app.WithDbContextAsync(async context =>
        {
            // Straight in the database: the domain would refuse to deactivate the last Platform Admin (L3).
            var admin = await context.Users.SingleAsync(Ct);
            context.Entry(admin).Property(u => u.IsActive).CurrentValue = false;
            return await context.SaveChangesAsync(Ct);
        });

        var (exitCode, _, _) = await RunAsync("demo-data");

        exitCode.ShouldBe(FlowzaCli.Refused);
    }

    [Fact]
    public async Task Help_lists_demo_data()
    {
        var help = await RunAsync("--help");

        help.Output.ShouldContain("demo-data");
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await FlowzaCli.RunAsync(args, output, error, s_settings);
        return (exitCode, output.ToString(), error.ToString());
    }
}
