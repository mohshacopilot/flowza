using Microsoft.Extensions.Configuration;

namespace Flowza.Testing;

/// <summary>
/// RabbitMQ connection for tests: the <c>flowza_test</c> virtual host, so tests never touch development messages.
/// Password from the <c>flowza-dev</c> user secrets or <c>FLOWZA_RabbitMq__Password</c>.
/// </summary>
public static class TestRabbitMq
{
    private static readonly Lazy<IConfiguration> s_configuration = new(() => new ConfigurationBuilder()
        .AddUserSecrets("flowza-dev")
        .AddEnvironmentVariables("FLOWZA_")
        .Build());

    public static string ConnectionString()
    {
        var configuration = s_configuration.Value;
        var password = configuration["RabbitMq:Password"];
        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException(
                "The RabbitMQ password for the tests is not set. Run docs\\setup\\set-secrets.ps1 (or set FLOWZA_RabbitMq__Password).");
        }

        var host = configuration["RabbitMq:Host"] ?? "localhost";
        var port = configuration["RabbitMq:Port"] ?? "5672";
        var user = configuration["RabbitMq:User"] ?? "flowza_app";
        var virtualHost = configuration["RabbitMq:TestVirtualHost"] ?? "flowza_test";
        return $"amqp://{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(password)}@{host}:{port}/{Uri.EscapeDataString(virtualHost)}";
    }
}
