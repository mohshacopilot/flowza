using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Flowza.Testing;

/// <summary>
/// Connection strings for the <c>flowza_test_*</c> databases (tech discussion TD1). Values come from the
/// <c>flowza-dev</c> user-secrets store on a developer PC, or from environment variables prefixed <c>FLOWZA_</c>
/// in CI (e.g. <c>FLOWZA_Sql__Password</c>). The password is never written to test output.
/// </summary>
public static class TestSqlServer
{
    private static readonly Lazy<IConfiguration> s_configuration = new(() => new ConfigurationBuilder()
        .AddUserSecrets("flowza-dev")
        .AddEnvironmentVariables("FLOWZA_")
        .Build());

    public static string ConnectionString(string database)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        var configuration = s_configuration.Value;

        var password = configuration["Sql:Password"];
        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException(
                "The SQL password for the tests is not set. Run: dotnet user-secrets set \"Sql:Password\" \"<password>\" --id flowza-dev " +
                "(or set the environment variable FLOWZA_Sql__Password). See docs/setup/local-setup.md.");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = configuration["Sql:Server"] ?? "localhost",
            InitialCatalog = database,
            UserID = configuration["Sql:User"] ?? "flowza_app",
            Password = password,
            Encrypt = true,
            TrustServerCertificate = !string.Equals(configuration["Sql:TrustServerCertificate"], "false", StringComparison.OrdinalIgnoreCase),
            ApplicationName = "Flowza tests",
        };
        return builder.ConnectionString;
    }
}
