using System.Net;
using System.Text.RegularExpressions;
using Flowza.Identity.Domain.Users;

namespace Flowza.Gateway.IntegrationTests.Infrastructure;

/// <summary>Drives a login and logout through the Gateway the way a browser does, one redirect at a time.</summary>
public static partial class Browser
{
    /// <summary>
    /// Follows redirects until a page answers, or until the next address is <paramref name="stopAt"/> (a path on the
    /// Gateway the test wants to look at itself). Returns the last response.
    /// </summary>
    public static async Task<HttpResponseMessage> FollowAsync(HttpClient browser, HttpResponseMessage response, string? stopAt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(response);
        for (var step = 0; step < 15 && IsRedirect(response.StatusCode); step++)
        {
            var next = new Uri(browser.BaseAddress!, response.Headers.Location!);
            if (stopAt is not null && next.PathAndQuery == stopAt)
            {
                return response;
            }

            response.Dispose();
            response = await browser.GetAsync(next, ct);
        }

        return response;
    }

    /// <summary>
    /// Starts at <c>/bff/login</c>, fills in Identity's login page (reached through the Gateway) and follows the code
    /// flow back to the Gateway. Returns the Gateway's final redirect to <paramref name="returnUrl"/>.
    /// </summary>
    public static async Task<HttpResponseMessage> LogInAsync(HttpClient browser, User user, string returnUrl, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(user);
        var loginPage = await FollowAsync(browser, await browser.GetAsync(new Uri($"/bff/login?returnUrl={Uri.EscapeDataString(returnUrl)}", UriKind.Relative), ct), returnUrl, ct);
        loginPage.StatusCode.ShouldBe(HttpStatusCode.OK, "the login page of Identity, served through the Gateway");
        var pageUri = loginPage.RequestMessage!.RequestUri!;
        pageUri.Authority.ShouldBe(browser.BaseAddress!.Authority, "the browser stays on the Gateway's address");

        var html = await loginPage.Content.ReadAsStringAsync(ct);
        var identityReturnUrl = Uri.UnescapeDataString(ReturnUrlParameter().Match(pageUri.Query).Groups[1].Value);
        var posted = await browser.PostAsync(pageUri, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = user.Username.Value,
            ["Password"] = GatewayPlatform.Password,
            ["ReturnUrl"] = identityReturnUrl,
            ["__RequestVerificationToken"] = AntiForgeryToken().Match(html).Groups[1].Value,
        }), ct);
        return await FollowAsync(browser, posted, returnUrl, ct);
    }

    private static bool IsRedirect(HttpStatusCode status) => (int)status is >= 300 and < 400;

    [GeneratedRegex("[?&]ReturnUrl=([^&]*)", RegexOptions.IgnoreCase)]
    private static partial Regex ReturnUrlParameter();

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiForgeryToken();
}
