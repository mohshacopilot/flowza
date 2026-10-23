using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Flowza.Gateway.IntegrationTests.Production;

/// <summary>T030: what a real installation needs from the Gateway (session keys, proxies in front, the built web app).</summary>
[Collection(ProductionGatewayGroup.Name)]
public sealed class ProductionGatewayTests(ProductionGatewayFiles files)
{
    private const string CookiePurpose = "session-cookie-check";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task T030_a_two_gateways_sharing_the_key_folder_read_each_others_session_cookies()
    {
        var keys = files.NewKeysFolder();
        await using var first = new ProductionGateway(files.Settings(keys));
        await using var second = new ProductionGateway(files.Settings(keys));
        await using var elsewhere = new ProductionGateway(files.Settings(files.NewKeysFolder()));

        var cookie = Protector(first).Protect("session of user 1");

        Protector(second).Unprotect(cookie).ShouldBe("session of user 1", "a restart or a second Gateway keeps the sessions");
        Should.Throw<CryptographicException>(() => Protector(elsewhere).Unprotect(cookie));
    }

    // Review finding 6: after a certificate renewal the sessions protected with the old certificate stay valid while
    // the old certificate stays installed (the setup guide says to keep it until its keys have expired).
    [Fact]
    public async Task T030_a_after_a_certificate_renewal_sessions_stay_valid_while_the_old_certificate_is_installed()
    {
        var keys = files.NewKeysFolder();
        string cookie;
        using (var old = new Flowza.Testing.TestCertificate("Flowza Gateway test keys (before renewal)"))
        {
            await using (var before = new ProductionGateway(files.Settings(keys, ("Gateway:DataProtection:CertificateThumbprint", old.Thumbprint))))
            {
                cookie = Protector(before).Protect("session of user 1");
            }

            await using var renewed = new ProductionGateway(files.Settings(keys, ("Gateway:DataProtection:CertificateThumbprint", files.RenewedThumbprint)));
            Protector(renewed).Unprotect(cookie).ShouldBe("session of user 1", "nobody is logged out by a renewal");
        }

        // The old certificate removed too early: its keys can no longer be read.
        await using var afterRemoval = new ProductionGateway(files.Settings(keys, ("Gateway:DataProtection:CertificateThumbprint", files.RenewedThumbprint)));
        Should.Throw<CryptographicException>(() => Protector(afterRemoval).Unprotect(cookie));
    }

    [Fact]
    public async Task T030_a_keys_on_disk_are_encrypted_with_the_certificate()
    {
        var keys = files.NewKeysFolder();
        await using var gateway = new ProductionGateway(files.Settings(keys));
        _ = Protector(gateway).Protect("anything"); // creates the first key

        var keyFiles = Directory.GetFiles(keys, "key-*.xml");
        keyFiles.ShouldNotBeEmpty();
        var xml = await File.ReadAllTextAsync(keyFiles[0], Ct);
        xml.ShouldContain("encryptedSecret");
        xml.ShouldContain("X509Certificate", customMessage: "encrypted for the configured certificate");
        xml.ShouldNotContain("<masterKey", customMessage: "the key itself is not readable on disk");
    }

    [Theory]
    [InlineData("Gateway:DataProtection:KeysFolder", null, "KeysFolder' is not set")]
    [InlineData("Gateway:DataProtection:Protection", "None", "must be protected")]
    public void T030_a_outside_development_the_gateway_refuses_to_start_without_protected_shared_keys(string key, string? value, string expected)
    {
        using var gateway = new ProductionGateway(files.Settings(files.NewKeysFolder(), (key, value)));

        var error = Should.Throw<InvalidOperationException>(() => gateway.Services);
        error.Message.ShouldContain(expected);
    }

    [Fact]
    public async Task T030_b_https_from_the_known_proxy_counts_and_the_same_header_from_anyone_else_is_ignored()
    {
        await using var gateway = new ProductionGateway(files.Settings(files.NewKeysFolder()));

        using var viaProxy = gateway.CreateBrowser(ProductionGateway.ProxyAddress);
        using var fromProxy = await viaProxy.SendAsync(Get("/health/live", forwardedProto: "https"), Ct);
        fromProxy.StatusCode.ShouldBe(HttpStatusCode.OK, "TLS ended at the load balancer: the request counts as https");

        using var direct = gateway.CreateBrowser("10.1.1.1");
        using var faked = await direct.SendAsync(Get("/health/live", forwardedProto: "https"), Ct);
        faked.StatusCode.ShouldBe(HttpStatusCode.TemporaryRedirect, "an unknown sender cannot claim https");
        faked.Headers.Location!.Scheme.ShouldBe("https");
    }

    [Fact]
    public async Task T030_b_IAM_07_AC2_rate_limits_see_the_client_address_behind_the_proxy()
    {
        await using var gateway = new ProductionGateway(files.Settings(
            files.NewKeysFolder(),
            ("Gateway:Protection:LoginPerAddress:PermitLimit", "1")));
        using var viaProxy = gateway.CreateBrowser(ProductionGateway.ProxyAddress);

        async Task<HttpStatusCode> PostLoginAsync(string client)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/account/login") { Content = new FormUrlEncodedContent([]) };
            request.Headers.Add("X-Forwarded-Proto", "https");
            request.Headers.Add("X-Forwarded-For", client);
            using var response = await viaProxy.SendAsync(request, Ct);
            return response.StatusCode;
        }

        (await PostLoginAsync("198.51.100.1")).ShouldNotBe(HttpStatusCode.TooManyRequests);
        (await PostLoginAsync("198.51.100.1")).ShouldBe(HttpStatusCode.TooManyRequests, "the same client again");
        (await PostLoginAsync("198.51.100.2")).ShouldNotBe(HttpStatusCode.TooManyRequests, "another client behind the same proxy has its own limit");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/admin/users")]
    [InlineData("/admin/users/0195f0c2-0000-7000-8000-000000000001")]
    public async Task GW2_the_built_web_app_answers_every_app_address_with_index_html(string path)
    {
        await using var gateway = new ProductionGateway(files.Settings(files.NewKeysFolder()));
        using var browser = gateway.CreateBrowser(ProductionGateway.ProxyAddress);

        using var response = await browser.SendAsync(Get(path, forwardedProto: "https"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain(ProductionGatewayFiles.IndexMarker);
        response.Headers.CacheControl!.NoCache.ShouldBeTrue("a new release must reach the browser at once");
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("script-src 'self';");
    }

    [Fact]
    public async Task GW2_built_files_are_served_and_cached_for_a_year()
    {
        await using var gateway = new ProductionGateway(files.Settings(files.NewKeysFolder()));
        using var browser = gateway.CreateBrowser(ProductionGateway.ProxyAddress);

        using var response = await browser.SendAsync(Get($"/assets/{ProductionGatewayFiles.AssetName}", forwardedProto: "https"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("console.log");
        response.Headers.CacheControl!.MaxAge.ShouldBe(TimeSpan.FromDays(365));
    }

    [Theory]
    [InlineData("/api/unknown/v1/things")]
    [InlineData("/bff/unknown")]
    [InlineData("/missing.png")]
    public async Task GW2_unknown_api_and_file_addresses_answer_404_not_the_app(string path)
    {
        await using var gateway = new ProductionGateway(files.Settings(files.NewKeysFolder()));
        using var browser = gateway.CreateBrowser(ProductionGateway.ProxyAddress);

        using var response = await browser.SendAsync(Get(path, forwardedProto: "https"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldNotContain(ProductionGatewayFiles.IndexMarker);
    }

    // Review finding 11 (not a defect: the fallback serves the file only for GET and HEAD); this keeps it that way.
    [Fact]
    public async Task GW2_a_post_to_an_unknown_address_is_not_answered_with_the_app()
    {
        await using var gateway = new ProductionGateway(files.Settings(files.NewKeysFolder()));
        using var browser = gateway.CreateBrowser(ProductionGateway.ProxyAddress);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/users") { Content = new StringContent("x") };
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await browser.SendAsync(request, Ct);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldNotContain(ProductionGatewayFiles.IndexMarker);
    }

    private static IDataProtector Protector(ProductionGateway gateway) =>
        gateway.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(CookiePurpose);

    private static HttpRequestMessage Get(string path, string forwardedProto)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Forwarded-Proto", forwardedProto);
        return request;
    }
}
