using System.Net;
using System.Text.Json;
using Flowza.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Flowza.BuildingBlocks.UnitTests.ServiceDefaults;

/// <summary>Health endpoints and error responses of a minimal app using the Flowza service defaults.</summary>
public sealed class ServiceDefaultsEndpointsTests : IAsyncLifetime
{
    private WebApplication? _app;
    private HttpClient? _client;
    private bool _databaseUp = true;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.AddFlowzaServiceDefaults();
        builder.Services.AddHealthChecks().AddCheck(
            "database",
            () => _databaseUp ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("connection refused"));

        _app = builder.Build();
        _app.UseFlowzaErrorHandling();
        _app.MapFlowzaDefaultEndpoints();
        _app.MapGet("/boom", (Func<IResult>)(() => throw new InvalidOperationException("internal detail")));

        await _app.StartAsync(Ct);
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task PLT_05_AC1_ready_reports_unhealthy_when_the_database_is_unreachable()
    {
        (await _client!.GetAsync(ServiceDefaultsExtensions.ReadyPath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        _databaseUp = false;
        var response = await _client.GetAsync(ServiceDefaultsExtensions.ReadyPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldContain("\"database\"");
        body.ShouldNotContain("connection refused");
    }

    [Fact]
    public async Task PLT_05_AC1_live_only_says_the_process_runs()
    {
        _databaseUp = false;

        (await _client!.GetAsync(ServiceDefaultsExtensions.LivePath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PLT_05_AC2_unexpected_errors_show_the_trace_reference_code_and_no_internal_details()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/boom");
        request.Headers.Add("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        var response = await _client!.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldNotContain("internal detail");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("reference").GetString().ShouldBe("4bf92f35");
    }
}
