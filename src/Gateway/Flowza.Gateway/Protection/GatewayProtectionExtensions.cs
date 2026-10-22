using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Flowza.Gateway.Protection;

/// <summary>
/// The Gateway's protection (task T021): anti-forgery header (SE5), identity-header stripping (GW4, in the proxy
/// transforms), security headers and HSTS (SE7), rate limits (GW3, L6) and the request body limit (GW5).
/// </summary>
public static class GatewayProtectionExtensions
{
    /// <summary>Rate-limiter policy for Identity's pages: password submissions per network address (IAM-07-AC2).</summary>
    public const string LoginPolicy = "login";

    /// <summary>Rate-limiter policy for the APIs and the BFF endpoints: requests per user (GW3).</summary>
    public const string PerUserPolicy = "per-user";

    public const string RateLimitedCode = "gateway.rate_limited";

    public static TBuilder AddGatewayProtection<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        var section = builder.Configuration.GetSection(GatewayProtectionOptions.Section);
        builder.Services.Configure<GatewayProtectionOptions>(section);
        var options = section.Get<GatewayProtectionOptions>() ?? new GatewayProtectionOptions();

        builder.Services.AddRateLimiter(limiter =>
        {
            // Only POSTs count: they are the password submissions; opening the page is free.
            limiter.AddPolicy(LoginPolicy, context => HttpMethods.IsPost(context.Request.Method)
                ? RateLimitPartition.GetFixedWindowLimiter($"address:{AddressOf(context)}", _ => FixedWindow(options.LoginPerAddress))
                : RateLimitPartition.GetNoLimiter("page"));

            limiter.AddPolicy(PerUserPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst("sub")?.Value is { } userId ? $"user:{userId}" : $"address:{AddressOf(context)}",
                _ => FixedWindow(options.ApiPerUser)));

            limiter.OnRejected = async (rejected, cancellationToken) =>
            {
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    rejected.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await Results.Problem(
                        detail: "Too many requests. Please wait a moment and try again.",
                        statusCode: StatusCodes.Status429TooManyRequests,
                        extensions: new Dictionary<string, object?> { ["code"] = RateLimitedCode })
                    .ExecuteAsync(rejected.HttpContext);
            };
        });

        if (!builder.Environment.IsDevelopment())
        {
            builder.Services.AddHsts(hsts =>
            {
                hsts.MaxAge = TimeSpan.FromDays(365);
                hsts.IncludeSubDomains = false;
            });
        }

        return builder;
    }

    /// <summary>Middleware that runs before authentication: HTTPS (outside Development), security headers and the body limit.</summary>
    public static WebApplication UseGatewayEdgeProtection(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.Environment.IsDevelopment())
        {
            // HTTP only in development (owner decision SE7 amendment); HTTPS everywhere else.
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<RequestBodyLimitMiddleware>();
        return app;
    }

    /// <summary>Middleware that needs the logged-in user: rate limits per user, then the anti-forgery header.</summary>
    public static WebApplication UseGatewayRequestProtection(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseRateLimiter();
        app.UseMiddleware<AntiForgeryHeaderMiddleware>();
        return app;
    }

    private static FixedWindowRateLimiterOptions FixedWindow(RateLimit limit) => new()
    {
        PermitLimit = limit.PermitLimit,
        Window = limit.Window,
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    // The address is only a partition key in memory; it is never logged (constitution 11.2).
    private static string AddressOf(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
