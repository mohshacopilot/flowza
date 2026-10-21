using Flowza.ServiceDefaults.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Flowza.BuildingBlocks.UnitTests.ServiceDefaults;

public sealed class SensitiveDataMaskingEnricherTests
{
    private readonly CollectingSink _sink = new();
    private readonly Logger _logger;

    public SensitiveDataMaskingEnricherTests() => _logger = new LoggerConfiguration()
        .Enrich.With<SensitiveDataMaskingEnricher>()
        .WriteTo.Sink(_sink)
        .CreateLogger();

    [Fact]
    public void PLT_05_AC3_personal_values_in_top_level_properties_are_masked()
    {
        _logger.Information("User {UserId} changed {Email} and {MobileNumber}", Guid.Empty, "ravi@bank.in", "9876543210");

        var properties = _sink.Single().Properties;
        properties["Email"].ToString().ShouldBe("\"***\"");
        properties["MobileNumber"].ToString().ShouldBe("\"***\"");
        properties["UserId"].ToString().ShouldContain("00000000");
    }

    [Fact]
    public void PLT_05_AC3_secrets_inside_logged_objects_are_masked()
    {
        var request = new { UserName = "ravi.k", Password = "Secret#123", Contact = new { Phone = "9876543210", City = "Chennai" } };

        _logger.Information("Request {@Request}", request);

        var rendered = _sink.Single().Properties["Request"].ToString();
        rendered.ShouldNotContain("Secret#123");
        rendered.ShouldNotContain("9876543210");
        rendered.ShouldContain("ravi.k");
        rendered.ShouldContain("Chennai");
    }

    [Fact]
    public void PLT_05_AC3_form_data_dictionaries_and_lists_are_masked()
    {
        var formData = new Dictionary<string, object> { ["LoanAmount"] = 1500000 };
        var items = new[] { new Dictionary<string, string> { ["Email"] = "a@b.in" } };

        _logger.Information("Saved {@FormData} and {@Items}", formData, items);

        var properties = _sink.Single().Properties;
        properties["FormData"].ToString().ShouldBe("\"***\"");
        properties["Items"].ToString().ShouldNotContain("a@b.in");
    }

    [Theory]
    [InlineData("Password", true)]
    [InlineData("connection_string", true)]
    [InlineData("AccessToken", true)]
    [InlineData("Pan", true)]
    [InlineData("PanNumber", true)]
    [InlineData("Aadhaar", true)]
    [InlineData("DateOfBirth", true)]
    [InlineData("Company", false)]
    [InlineData("SpanId", false)]
    [InlineData("RequestNumber", false)]
    [InlineData("UserId", false)]
    public void T7_OB2_sensitive_names_are_recognised(string name, bool expected)
    {
        SensitiveDataMaskingEnricher.IsSensitive(name).ShouldBe(expected);
    }

    private sealed class CollectingSink : ILogEventSink
    {
        private readonly List<LogEvent> _events = [];

        public void Emit(LogEvent logEvent) => _events.Add(logEvent);

        public LogEvent Single() => _events.Single();
    }
}
