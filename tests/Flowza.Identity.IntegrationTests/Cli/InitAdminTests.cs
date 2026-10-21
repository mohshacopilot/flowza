using System.Net;
using System.Text.RegularExpressions;
using Flowza.Cli;
using Flowza.Identity.Domain.Users;
using Flowza.Identity.IntegrationTests.Connect;
using Flowza.Identity.IntegrationTests.Infrastructure;
using Flowza.Identity.Infrastructure.Persistence;
using Flowza.Testing;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Identity.IntegrationTests.Cli;

/// <summary>T019 / IAM-03 (C2, PA5): <c>flowza init-admin</c> on <c>flowza_test_identity</c>.</summary>
[Collection(IdentityTestGroup.Name)]
public sealed partial class InitAdminTests(IdentityApp app) : IAsyncLifetime
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
    public async Task IAM_03_AC1_a_new_installation_gets_one_admin_with_a_generated_password_shown_once_that_must_be_changed()
    {
        var (exitCode, output, _) = await RunAsync("init-admin", "--email", "admin@bank.example");

        exitCode.ShouldBe(FlowzaCli.Ok);
        var password = OneTimePassword().Match(output).Groups[1].Value;
        password.Length.ShouldBe(16, "a strong random password (C2), not a well-known default");

        var users = await app.WithDbContextAsync(context => context.Users.AsNoTracking().ToListAsync(Ct));
        var admin = users.ShouldHaveSingleItem();
        admin.Username.Value.ShouldBe("admin");
        admin.Roles.ShouldContain(SystemRole.PlatformAdmin);
        admin.IsActive.ShouldBeTrue();
        admin.MustChangePassword.ShouldBeTrue();

        var hash = await app.WithDbContextAsync(context => context.Users
            .Select(u => EF.Property<string>(u, UserCredentialColumns.PasswordHash)).SingleAsync(Ct));
        hash.ShouldNotContain(password, customMessage: "only a hash is stored (constitution 7.7)");

        // The first login leads straight to the forced password change (IAM-03).
        using var browser = app.CreateBrowser();
        var login = await LoginBrowser.PostLoginAsync(browser, "admin", password, "/", Ct);
        login.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        login.Headers.Location!.ToString().ShouldStartWith("/account/change-password");
    }

    [Fact]
    public async Task IAM_03_AC1_a_second_run_refuses_and_changes_nothing()
    {
        (await RunAsync("init-admin", "--email", "admin@bank.example")).ExitCode.ShouldBe(FlowzaCli.Ok);

        var (exitCode, output, error) = await RunAsync("init-admin", "--email", "second@bank.example", "--username", "second.admin");

        exitCode.ShouldBe(FlowzaCli.Refused);
        error.ShouldContain("already exists");
        output.ShouldNotContain("One-time password");
        (await app.WithDbContextAsync(context => context.Users.CountAsync(Ct))).ShouldBe(1);
    }

    [Fact]
    public async Task The_email_is_required_and_bad_values_are_refused_by_the_same_rules_as_the_API()
    {
        (await RunAsync("init-admin")).ExitCode.ShouldBe(FlowzaCli.UsageError);
        (await RunAsync("init-admin", "--email", "not-an-email")).ExitCode.ShouldBe(FlowzaCli.Refused);
        (await RunAsync("init-admin", "--email", "a@b.example", "--username", "x")).ExitCode.ShouldBe(FlowzaCli.Refused);
        (await app.WithDbContextAsync(context => context.Users.CountAsync(Ct))).ShouldBe(0);
    }

    [Fact]
    public async Task Unknown_commands_and_help()
    {
        (await RunAsync("make-coffee")).ExitCode.ShouldBe(FlowzaCli.UsageError);
        var help = await RunAsync("--help");
        help.ExitCode.ShouldBe(FlowzaCli.Ok);
        help.Output.ShouldContain("init-admin");
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await FlowzaCli.RunAsync(args, output, error, s_settings);
        return (exitCode, output.ToString(), error.ToString());
    }

    [GeneratedRegex("One-time password: (\\S+)")]
    private static partial Regex OneTimePassword();
}
