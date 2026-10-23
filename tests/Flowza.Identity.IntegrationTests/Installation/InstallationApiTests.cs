using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flowza.Identity.Api;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Flowza.Identity.IntegrationTests.Installation;

/// <summary>T003: the installation name set at installation, and <c>GET /api/v1/installation</c> (UI-08, C7; PA1, PA2).</summary>
[Collection(IdentityTestGroup.Name)]
public sealed class InstallationApiTests(IdentityApp app) : IAsyncLifetime
{
    private static readonly Uri s_installation = new("/api/v1/installation", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await app.ResetDataAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task UI_08_AC2_without_a_configured_name_a_logged_in_user_gets_Flowza_and_FL()
    {
        var (_, session) = await app.UserWithSessionAsync("ravi.k");
        using var _ = session;

        using var response = await session.Client.GetAsync(s_installation, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("name").GetString().ShouldBe("Flowza");
        body.GetProperty("initials").GetString().ShouldBe("FL");
        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl.Private.ShouldBeTrue();
        response.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task UI_08_AC2_anonymous_callers_are_refused()
    {
        using var anonymous = app.CreateClient();

        (await anonymous.GetAsync(s_installation, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UI_08_the_endpoint_is_in_Identitys_OpenAPI_document_for_the_web_client()
    {
        using var anonymous = app.CreateClient();

        var document = await anonymous.GetFromJsonAsync<JsonElement>(new Uri("/openapi/v1.json", UriKind.Relative), Ct);

        var operation = document.GetProperty("paths").GetProperty("/api/v1/installation").GetProperty("get");
        operation.GetProperty("operationId").GetString().ShouldBe("GetInstallation");
    }

    [Fact]
    public async Task UI_08_AC2_the_name_set_at_installation_is_used_with_its_initials()
    {
        await using var configured = WithInstallationName("State Bank of Example");

        var brand = await configured.Services.GetRequiredService<IInstallationBrandSource>().GetAsync(Ct);

        brand.Name.ShouldBe("State Bank of Example");
        brand.Initials.ShouldBe("SB");
    }

    public static TheoryData<string> WrongNames => ["   ", new string('a', 101)];

    [Theory]
    [MemberData(nameof(WrongNames))]
    public async Task UI_08_the_service_refuses_to_start_with_a_blank_or_too_long_name(string name)
    {
        await using var misconfigured = WithInstallationName(name);

        var error = Should.Throw<OptionsValidationException>(() => misconfigured.Services);

        error.Message.ShouldContain("Installation:Name");
    }

    /// <summary>A second Identity host on the same test database with another installation name.</summary>
    private WebApplicationFactory<ApiAssembly> WithInstallationName(string name) =>
        app.WithWebHostBuilder(builder => builder.UseSetting("Installation:Name", name));
}
