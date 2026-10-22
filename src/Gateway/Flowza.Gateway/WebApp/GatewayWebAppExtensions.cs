namespace Flowza.Gateway.WebApp;

/// <summary>
/// GW2: outside Development the Gateway serves the built React app itself (from its <c>wwwroot</c>, copied there by
/// <c>dotnet publish</c> from <c>src/Web/dist</c>; task T030 item d). Every other path that is not a file gets
/// <c>index.html</c>, so the app's own routes (/admin/users, /me, …) work after a reload. Paths under /api and /bff
/// that no route took answer 404 as ProblemDetails, never with the app's HTML. In Development the Vite dev server
/// serves the app through a proxy route instead (appsettings.Development.json).
/// </summary>
public static class GatewayWebAppExtensions
{
    private const string IndexFile = "index.html";

    /// <summary>Vite puts a content hash in every file name under /assets, so those files never change.</summary>
    private static readonly StaticFileOptions s_files = new()
    {
        OnPrepareResponse = context =>
        {
            var headers = context.Context.Response.Headers;
            headers.CacheControl = context.Context.Request.Path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase)
                ? "public, max-age=31536000, immutable"
                : "no-cache";
        },
    };

    private static bool Enabled(WebApplication app) => !app.Environment.IsDevelopment();

    /// <summary>The built files (scripts, styles, images); before authentication, as they need no login.</summary>
    public static WebApplication UseGatewayWebAppFiles(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!Enabled(app))
        {
            return app;
        }

        if (!app.Environment.WebRootFileProvider.GetFileInfo(IndexFile).Exists)
        {
            GatewayStartupLog.WebAppMissing(app.Logger);
        }

        app.UseStaticFiles(s_files);
        return app;
    }

    /// <summary>The fallbacks; they come after every other route (lowest priority).</summary>
    public static WebApplication MapGatewayWebApp(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!Enabled(app))
        {
            return app;
        }

        app.MapFallback("/api/{**path}", NotFound);
        app.MapFallback("/bff/{**path}", NotFound);
        app.MapFallbackToFile(IndexFile, s_files);
        return app;
    }

    private static Task NotFound(HttpContext context) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "There is nothing at this address.")
            .ExecuteAsync(context);
}
