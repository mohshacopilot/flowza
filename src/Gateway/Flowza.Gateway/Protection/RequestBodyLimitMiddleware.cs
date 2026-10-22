using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace Flowza.Gateway.Protection;

/// <summary>
/// GW5: request bodies are at most 1 MB (configurable). A declared larger body is refused at once with 413; a body
/// without a declared length is cut off by the server (Kestrel or IIS) at the same limit. File uploads get their own,
/// per-field limit with feature 004.
/// </summary>
internal sealed class RequestBodyLimitMiddleware(RequestDelegate next, IOptions<GatewayProtectionOptions> options)
{
    public const string ErrorCode = "gateway.request_too_large";

    private readonly long _limit = options.Value.MaxRequestBodyBytes;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentLength > _limit)
        {
            await Results.Problem(
                    detail: "The request is too large.",
                    statusCode: StatusCodes.Status413PayloadTooLarge,
                    extensions: new Dictionary<string, object?> { ["code"] = ErrorCode })
                .ExecuteAsync(context);
            return;
        }

        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodySize)
        {
            bodySize.MaxRequestBodySize = _limit;
        }

        await next(context);
    }
}
