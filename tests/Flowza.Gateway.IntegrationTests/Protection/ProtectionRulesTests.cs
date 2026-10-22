using Flowza.Gateway.Protection;
using Microsoft.AspNetCore.Http;

namespace Flowza.Gateway.IntegrationTests.Protection;

/// <summary>Unit tests of the protection rules (no server needed).</summary>
public sealed class ProtectionRulesTests
{
    [Theory]
    [InlineData("POST", "/api/identity/v1/users", true)]
    [InlineData("PUT", "/api/design/v1/apps/1", true)]
    [InlineData("PATCH", "/api/runtime/v1/x", true)]
    [InlineData("DELETE", "/API/worker/v1/x", true)]
    [InlineData("POST", "/bff/anything", true)]
    [InlineData("GET", "/api/identity/v1/users", false)]
    [InlineData("HEAD", "/api/identity/v1/users", false)]
    [InlineData("OPTIONS", "/api/identity/v1/users", false)]
    [InlineData("POST", "/account/login", false)]
    [InlineData("POST", "/connect/token", false)]
    [InlineData("POST", "/apis-not-ours", false)]
    public void SE5_anti_forgery_header_is_needed_for_changes_to_apis_and_bff(string method, string path, bool needed)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        AntiForgeryHeaderMiddleware.NeedsHeader(context.Request).ShouldBe(needed);
    }

    [Theory]
    [InlineData("Authorization", true)]
    [InlineData("x-user-id", true)]
    [InlineData("X-MS-CLIENT-PRINCIPAL-NAME", true)]
    [InlineData("X-Flowza-Anything", true)]
    [InlineData("Accept", false)]
    [InlineData("traceparent", false)]
    [InlineData("X-CSRF", false)]
    public void GW4_identity_headers_are_recognised(string name, bool isIdentityHeader)
    {
        IdentityHeaders.IsIdentityHeader(name).ShouldBe(isIdentityHeader);
    }

    [Fact]
    public void SE7_content_security_policy_is_strict_outside_development_and_configurable()
    {
        var defaults = new GatewayProtectionOptions();
        SecurityHeaders.ContentSecurityPolicyFor(defaults, isDevelopment: false).ShouldBe(SecurityHeaders.ProductionContentSecurityPolicy);
        SecurityHeaders.ProductionContentSecurityPolicy.ShouldContain("script-src 'self';");
        SecurityHeaders.ProductionContentSecurityPolicy.ShouldContain("frame-ancestors 'none'");
        SecurityHeaders.ContentSecurityPolicyFor(defaults, isDevelopment: true).ShouldBe(SecurityHeaders.DevelopmentContentSecurityPolicy);

        var configured = new GatewayProtectionOptions { ContentSecurityPolicy = "default-src 'none'" };
        SecurityHeaders.ContentSecurityPolicyFor(configured, isDevelopment: false).ShouldBe("default-src 'none'");
    }

    [Fact]
    public void GW5_default_limits_are_the_M1_values()
    {
        var defaults = new GatewayProtectionOptions();
        defaults.MaxRequestBodyBytes.ShouldBe(1024 * 1024);
        defaults.LoginPerAddress.PermitLimit.ShouldBe(10);
        defaults.LoginPerAddress.Window.ShouldBe(TimeSpan.FromMinutes(1));
        defaults.ApiPerUser.PermitLimit.ShouldBe(600);
    }
}
