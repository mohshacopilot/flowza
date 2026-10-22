using Microsoft.Extensions.Options;

namespace Flowza.Gateway.Protection;

/// <summary>
/// SE7: security headers on every answer of the Gateway, including those of Identity's pages and the web app,
/// whose own values are replaced. HSTS is added separately, outside Development only (HTTPS).
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// Same-origin only; no framing (<c>frame-ancestors 'none'</c>); styles may be inline because Ant Design writes
    /// its styles at run time; images and fonts may be data: URLs.
    /// </summary>
    public const string ProductionContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; "
        + "connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    /// <summary>Development adds what Vite's dev server needs: its inline React refresh script and its hot-reload web socket.</summary>
    public const string DevelopmentContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; "
        + "connect-src 'self' ws: wss:; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    public static string ContentSecurityPolicyFor(GatewayProtectionOptions options, bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.ContentSecurityPolicy is { Length: > 0 } configured
            ? configured
            : isDevelopment ? DevelopmentContentSecurityPolicy : ProductionContentSecurityPolicy;
    }
}

internal sealed class SecurityHeadersMiddleware(RequestDelegate next, IOptions<GatewayProtectionOptions> options, IHostEnvironment environment)
{
    private readonly string _contentSecurityPolicy = SecurityHeaders.ContentSecurityPolicyFor(options.Value, environment.IsDevelopment());

    public Task InvokeAsync(HttpContext context)
    {
        // Set when the answer starts, so values copied from a proxied answer are overwritten.
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = _contentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";

            // Login answers carry a code in the address; no other site should see addresses of this one.
            headers["Referrer-Policy"] = "no-referrer";
            return Task.CompletedTask;
        });
        return next(context);
    }
}
