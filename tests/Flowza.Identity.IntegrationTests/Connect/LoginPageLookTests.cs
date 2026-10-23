using System.Net;
using Flowza.Identity.Api;
using Flowza.Identity.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Flowza.Identity.IntegrationTests.Connect;

/// <summary>T004: the login and change-password pages in the split layout with the installation's brand (UI-01, UI-08, C1, C5, C7).</summary>
[Collection(IdentityTestGroup.Name)]
public sealed class LoginPageLookTests(IdentityApp app) : IAsyncLifetime
{
    private static readonly Uri s_login = new("/account/login", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await app.ResetDataAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task UI_01_AC1_the_login_page_shows_the_brand_panel_the_form_and_Powered_by_Flowza()
    {
        using var browser = app.CreateBrowser();

        var html = await browser.GetStringAsync(s_login, Ct);

        ShouldHaveTheSplitLayout(html);
        html.ShouldContain("<span class=\"brand-mark\" aria-hidden=\"true\">FL</span>");
        html.ShouldContain("<span class=\"brand-name\" title=\"Flowza\">Flowza</span>");
        html.ShouldContain("<p class=\"product-line\">Requests and approvals in one place</p>");
        html.ShouldContain("<h1>Log in</h1>");
        html.IndexOf("<form method=\"post\"", StringComparison.Ordinal).ShouldBeGreaterThan(html.IndexOf("class=\"form-column\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UI_02_AC2_the_login_pages_load_the_generated_design_values_before_their_layout()
    {
        using var browser = app.CreateBrowser();

        var html = await browser.GetStringAsync(s_login, Ct);
        var designValues = await browser.GetAsync(new Uri("/account/assets/design-values.css", UriKind.Relative), Ct);

        var designValuesLink = html.IndexOf("href=\"/account/assets/design-values.css\"", StringComparison.Ordinal);
        designValuesLink.ShouldBeGreaterThan(0);
        designValuesLink.ShouldBeLessThan(html.IndexOf("href=\"/account/assets/account.css\"", StringComparison.Ordinal));
        designValues.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await designValues.Content.ReadAsStringAsync(Ct)).ShouldContain("--flowza-brand: #1f5fbf;");
    }

    [Fact]
    public async Task UI_01_AC2_the_pages_load_the_show_password_script_from_a_file_with_its_translated_name()
    {
        using var browser = app.CreateBrowser();

        var html = await browser.GetStringAsync(s_login, Ct);
        var script = await browser.GetAsync(new Uri("/account/assets/account.js", UriKind.Relative), Ct);

        // A file, not an inline script: the Gateway's Content-Security-Policy allows only scripts from its own address.
        html.ShouldContain("<script src=\"/account/assets/account.js\" defer></script>");
        html.ShouldNotContain("<script>");
        html.ShouldContain("<body data-show-password-label=\"Show password\">");
        script.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await script.Content.ReadAsStringAsync(Ct)).ShouldContain("aria-pressed");
    }

    [Fact]
    public async Task UI_01_AC3_a_failed_login_shows_the_general_message_above_the_form_in_the_page_look()
    {
        await app.CreateUserAsync("ravi.k");
        using var browser = app.CreateBrowser();

        var response = await LoginBrowser.PostLoginAsync(browser, "ravi.k", "Wrong-Password-1", null, Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);

        LoginBrowser.AlertText(html).ShouldBe("Username or password is incorrect.");
        ShouldHaveTheSplitLayout(html);
        var alert = html.IndexOf("class=\"errors\" role=\"alert\"", StringComparison.Ordinal);
        alert.ShouldBeGreaterThan(html.IndexOf("<main id=\"main\" class=\"form-area\">", StringComparison.Ordinal));
        alert.ShouldBeLessThan(html.IndexOf("<form method=\"post\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UI_01_AC3_the_change_password_page_has_the_same_look_as_the_login_page()
    {
        await app.CreateUserAsync("new.user", password: "Temporary-Pass-1", mustChangePassword: true);
        using var browser = app.CreateBrowser();
        var login = await LoginBrowser.PostLoginAsync(browser, "new.user", "Temporary-Pass-1", null, Ct);
        var changePage = login.Headers.Location!.ToString();
        changePage.ShouldStartWith("/account/change-password");

        var html = await browser.GetStringAsync(new Uri(changePage, UriKind.Relative), Ct);

        ShouldHaveTheSplitLayout(html);
        html.ShouldContain("<h1>Choose a new password</h1>");
        html.ShouldContain("<span class=\"brand-mark\" aria-hidden=\"true\">FL</span>");
    }

    [Fact]
    public async Task UI_08_AC2_the_login_page_shows_the_name_set_at_installation_and_its_initials()
    {
        await using var configured = WithInstallationName("State Bank of Example");
        using var browser = configured.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var html = await browser.GetStringAsync(s_login, Ct);

        html.ShouldContain("<title>Log in – State Bank of Example</title>");
        html.ShouldContain("<span class=\"brand-mark\" aria-hidden=\"true\">SB</span>");
        html.ShouldContain("<span class=\"brand-name\" title=\"State Bank of Example\">State Bank of Example</span>");
        html.ShouldContain("Powered by Flowza");
    }

    [Fact]
    public async Task UI_08_AC1_a_100_character_name_keeps_its_full_text_for_hover_and_screen_readers()
    {
        // The panel shortens it with "…" (CSS); the page itself always carries the whole name.
        var name = "Directorate of " + new string('x', 85);
        name.Length.ShouldBe(100);
        await using var configured = WithInstallationName(name);
        using var browser = configured.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var html = await browser.GetStringAsync(s_login, Ct);

        html.ShouldContain($"<span class=\"brand-name\" title=\"{name}\">{name}</span>");
        html.ShouldContain("<span class=\"brand-mark\" aria-hidden=\"true\">DO</span>");
    }

    private static void ShouldHaveTheSplitLayout(string html)
    {
        var panel = html.IndexOf("<header class=\"brand-panel\">", StringComparison.Ordinal);
        var column = html.IndexOf("<div class=\"form-column\">", StringComparison.Ordinal);
        panel.ShouldBeGreaterThan(0, "the brand panel is missing");
        column.ShouldBeGreaterThan(panel, "the form column must follow the brand panel");
        html.IndexOf("<footer class=\"powered-by\">Powered by Flowza</footer>", StringComparison.Ordinal).ShouldBeGreaterThan(column);
    }

    /// <summary>A second Identity host on the same test database with another installation name.</summary>
    private WebApplicationFactory<ApiAssembly> WithInstallationName(string name) =>
        app.WithWebHostBuilder(builder => builder.UseSetting("Installation:Name", name));
}
