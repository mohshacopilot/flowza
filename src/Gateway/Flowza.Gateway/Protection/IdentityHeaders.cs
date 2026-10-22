using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;

namespace Flowza.Gateway.Protection;

/// <summary>
/// GW4: headers that claim who the caller is never pass from the browser to a service. Services trust only the
/// access token the Gateway attaches (SE1). YARP itself replaces any incoming <c>X-Forwarded-*</c> values and carries
/// the trace on with a fresh <c>traceparent</c> from the Gateway's own activity.
/// </summary>
public static class IdentityHeaders
{
    /// <summary>Header names some proxies and hosting platforms use to pass a user on.</summary>
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        HeaderNames.Authorization,
        "Remote-User",
        "X-Remote-User",
        "X-User",
        "X-User-Id",
        "X-User-Name",
        "X-User-Email",
        "X-User-Roles",
        "X-Forwarded-User",
        "X-Forwarded-Email",
        "X-Auth-Request-User",
        "X-Auth-Request-Email",
        "X-MS-CLIENT-PRINCIPAL",
        "X-MS-CLIENT-PRINCIPAL-ID",
        "X-MS-CLIENT-PRINCIPAL-NAME",
        "X-MS-CLIENT-PRINCIPAL-IDP",
    };

    /// <summary>Flowza reserves this prefix for its own internal headers; the browser may not send any.</summary>
    public const string ReservedPrefix = "X-Flowza-";

    public static bool IsIdentityHeader(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Names.Contains(name) || name.StartsWith(ReservedPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>YARP request transform; it runs before the transform that attaches the session's token.</summary>
    internal static ValueTask RemoveAsync(RequestTransformContext context)
    {
        var headers = context.ProxyRequest.Headers;
        foreach (var name in headers.Select(header => header.Key).Where(IsIdentityHeader).ToList())
        {
            headers.Remove(name);
        }

        return ValueTask.CompletedTask;
    }
}
