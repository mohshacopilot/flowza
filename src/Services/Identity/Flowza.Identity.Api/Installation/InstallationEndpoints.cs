using Flowza.Identity.Application.Installation.GetInstallation;
using Microsoft.Net.Http.Headers;
using Wolverine;

namespace Flowza.Identity.Api.Installation;

/// <summary>The installation's name and initials mark for the web app's frame (UI-08, PA1).</summary>
public static class InstallationEndpoints
{
    /// <summary>The name changes only at installation (C7), so the browser may keep it for a while.</summary>
    public static readonly TimeSpan BrowserCacheTime = TimeSpan.FromMinutes(5);

    public static IEndpointRouteBuilder MapInstallationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Any logged-in user (IAM-05): every screen of the frame shows it.
        app.MapGet("/api/v1/installation", async (HttpContext context, IMessageBus bus, CancellationToken ct) =>
            {
                var installation = await bus.InvokeAsync<InstallationView>(new GetInstallationQuery(), ct);
                context.Response.Headers[HeaderNames.CacheControl] = $"private, max-age={(int)BrowserCacheTime.TotalSeconds}";
                return TypedResults.Ok(installation);
            })
            .RequireAuthorization()
            .WithTags("Installation")
            .WithName("GetInstallation")
            .Produces<InstallationView>();

        return app;
    }
}
