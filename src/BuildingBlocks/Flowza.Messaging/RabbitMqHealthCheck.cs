using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Flowza.Messaging;

/// <summary>Readiness: can this service open a connection to RabbitMQ? (PLT-05, tech discussion Q-P1).</summary>
internal sealed class RabbitMqHealthCheck(Uri connectionUri) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new ConnectionFactory { Uri = connectionUri, RequestedConnectionTimeout = TimeSpan.FromSeconds(5) };
            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("RabbitMQ connection is not open.");
        }
#pragma warning disable CA1031 // A health check reports any failure instead of throwing.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            // The message is logged by the health check system; the HTTP response does not show it.
            return HealthCheckResult.Unhealthy("RabbitMQ is not reachable.", exception);
        }
    }
}
