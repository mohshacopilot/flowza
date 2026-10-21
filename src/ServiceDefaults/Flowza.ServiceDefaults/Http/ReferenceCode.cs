using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Flowza.ServiceDefaults.Http;

/// <summary>
/// The short reference shown to users on error screens: the first 8 characters of the request's trace ID
/// (tech discussion T7, OB4). Support finds the full trace in the logs by searching for it.
/// </summary>
public static class ReferenceCode
{
    public const int Length = 8;

    public static string From(string traceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(traceId);
        return traceId.Length <= Length ? traceId : traceId[..Length];
    }

    public static string For(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var traceId = Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
        return From(traceId);
    }
}
