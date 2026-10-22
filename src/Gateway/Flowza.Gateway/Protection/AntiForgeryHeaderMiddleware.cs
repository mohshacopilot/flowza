namespace Flowza.Gateway.Protection;

/// <summary>
/// SE5: a change request (POST, PUT, PATCH, DELETE) to the APIs or the BFF endpoints must carry <c>X-CSRF: 1</c>.
/// Another site can make a browser send a form, but not add a custom header without the Gateway's CORS permission,
/// which it never gives; the SameSite=Strict cookie is the second guard. The web app adds the header in its one fetch
/// function (<c>src/Web/src/shared/api/apiFetch.ts</c>). Identity's own pages protect their forms with their own tokens.
/// </summary>
internal sealed class AntiForgeryHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-CSRF";
    public const string HeaderValue = "1";
    public const string ErrorCode = "gateway.antiforgery_header_missing";

    private static readonly PathString[] s_protectedPaths = ["/api", "/bff"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (NeedsHeader(context.Request) && context.Request.Headers[HeaderName] != HeaderValue)
        {
            await Results.Problem(
                    detail: "The request was refused because it lacks the anti-forgery header.",
                    statusCode: StatusCodes.Status403Forbidden,
                    extensions: new Dictionary<string, object?> { ["code"] = ErrorCode })
                .ExecuteAsync(context);
            return;
        }

        await next(context);
    }

    internal static bool NeedsHeader(HttpRequest request) =>
        !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method))
        && s_protectedPaths.Any(path => request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase));
}
