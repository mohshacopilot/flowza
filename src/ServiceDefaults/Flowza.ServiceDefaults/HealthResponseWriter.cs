using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Flowza.ServiceDefaults;

/// <summary>
/// Writes the readiness result as small JSON: overall status and each check's status and non-secret details
/// (e.g. which message transport the service uses, ADR-0005). Exception messages are not shown (constitution 10.3).
/// </summary>
internal static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new { status = e.Value.Status.ToString(), data = e.Value.Data.ToDictionary(d => d.Key, d => d.Value?.ToString()) }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, s_json));
    }
}
