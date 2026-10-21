using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowza.Platform.IntegrationTests.Services;

/// <summary>
/// PLT-05-AC3 / NFR-07: a real service's log files never contain personal values, even when code passes them to
/// the logger by mistake (the masking policy is the second line of defence; constitution 11.2).
/// </summary>
[Collection(ServiceDatabasesTestGroup.Name)]
public sealed class LogPrivacyTests
{
    private const string Email = "ravi.secret.test@bank.example";
    private const string Password = "S3cret!Pass-not-for-logs";
    private const string Mobile = "9876501234";

    [Fact]
    public async Task PLT_05_AC3_personal_values_never_reach_the_log_files_and_PLT_05_AC2_lines_carry_the_trace_reference()
    {
        var folder = Path.Combine(Path.GetTempPath(), "flowza-log-scan", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("Flowza__Logging__Folder", folder);
        var userId = Guid.NewGuid();
        string traceId;
        try
        {
            await using (var app = ServiceApps.Create("identity"))
            {
                using var client = app.CreateClient();
                var services = ((Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Flowza.Identity.Api.ApiAssembly>)app).Services;
                var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("LogPrivacyTest");
                using var source = new ActivitySource("Flowza.Tests");
                using var userAction = source.StartActivity("user action");
                traceId = userAction!.TraceId.ToHexString();
#pragma warning disable CA1848, CA1873, CA2254 // Deliberately "wrong" logging to prove the guard works.
                logger.LogInformation("User {UserId} changed {Email}, {Password} and {MobileNumber}", userId, Email, Password, Mobile);
                logger.LogWarning("Request {@Request}", new { UserId = userId, Contact = new { Email, Phone = Mobile } });
#pragma warning restore CA1848, CA1873, CA2254
            }

            var text = string.Join("\n", Directory.GetFiles(folder, "*.log").Select(ReadShared));

            text.ShouldContain(userId.ToString());
            text.ShouldContain(traceId[..8], customMessage: "the reference code (OB4) must be findable in the log files");
            text.ShouldNotContain(Email);
            text.ShouldNotContain(Password);
            text.ShouldNotContain(Mobile);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Flowza__Logging__Folder", null);
        }
    }

    // The logger may still hold the file open; Serilog writes each line immediately, so a shared read sees it all.
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
