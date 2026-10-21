using System.Text.RegularExpressions;

namespace Flowza.Identity.IntegrationTests.Connect;

/// <summary>Fills in Identity's own pages like a browser (form fields + anti-forgery token).</summary>
public static partial class LoginBrowser
{
    public static async Task<HttpResponseMessage> PostLoginAsync(HttpClient browser, string username, string password, string? returnUrl, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(browser);
        var page = "/account/login" + (returnUrl is null ? string.Empty : $"?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        return await PostFormAsync(browser, page, new Dictionary<string, string>
        {
            ["Username"] = username,
            ["Password"] = password,
            ["ReturnUrl"] = returnUrl ?? string.Empty,
        }, ct);
    }

    public static async Task<HttpResponseMessage> PostFormAsync(HttpClient browser, string page, Dictionary<string, string> fields, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(fields);
        var html = await browser.GetStringAsync(new Uri(page, UriKind.Relative), ct);
        fields["__RequestVerificationToken"] = AntiForgeryToken().Match(html).Groups[1].Value;
        return await browser.PostAsync(new Uri(page, UriKind.Relative), new FormUrlEncodedContent(fields), ct);
    }

    /// <summary>The text of the page's alert box, if any.</summary>
    public static string? AlertText(string html)
    {
        var match = Alert().Match(html);
        return match.Success ? Regex.Replace(match.Groups[1].Value, "<[^>]+>", " ").Trim() : null;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiForgeryToken();

    [GeneratedRegex("role=\"alert\"[^>]*>(.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex Alert();
}
