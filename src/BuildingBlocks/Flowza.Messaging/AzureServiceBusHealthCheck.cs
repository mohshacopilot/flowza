using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Flowza.Messaging;

/// <summary>
/// Readiness: can this service reach its Azure Service Bus namespace? (PLT-05; ADR-0005). Coded now, tested once
/// Azure access exists.
/// </summary>
internal sealed class AzureServiceBusHealthCheck(string connectionString) : IHealthCheck
{
    private readonly ServiceBusAdministrationClient _client = new(connectionString);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetNamespacePropertiesAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
#pragma warning disable CA1031 // A health check reports any failure instead of throwing.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return HealthCheckResult.Unhealthy("Azure Service Bus is not reachable.", exception);
        }
    }
}
