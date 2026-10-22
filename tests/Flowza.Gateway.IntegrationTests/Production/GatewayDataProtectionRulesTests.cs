using Flowza.Gateway.Session;

namespace Flowza.Gateway.IntegrationTests.Production;

/// <summary>T030 item a: which key settings let the Gateway start (unit tests of the rules, no server).</summary>
public sealed class GatewayDataProtectionRulesTests
{
    private static GatewayDataProtectionOptions Options(string? folder, KeyProtection protection, string? thumbprint = null) =>
        new() { KeysFolder = folder, Protection = protection, CertificateThumbprint = thumbprint };

    [Fact]
    public void Development_may_keep_the_keys_with_the_developer_account()
    {
        GatewayDataProtection.Check(Options(null, KeyProtection.None), isDevelopment: true, isWindows: true).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_shared_folder_with_a_certificate_works_on_any_system(bool isWindows)
    {
        GatewayDataProtection.Check(Options(@"\\files\flowza-keys", KeyProtection.Certificate, "ABC123"), isDevelopment: false, isWindows)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Dpapi_works_on_windows_only()
    {
        GatewayDataProtection.Check(Options(@"C:\flowza\keys", KeyProtection.Dpapi), isDevelopment: false, isWindows: true).ShouldBeEmpty();
        GatewayDataProtection.Check(Options("/var/flowza/keys", KeyProtection.Dpapi), isDevelopment: false, isWindows: false)
            .ShouldHaveSingleItem().ShouldContain("Windows only");
    }

    [Fact]
    public void Outside_development_a_missing_folder_and_unprotected_keys_are_both_reported()
    {
        var problems = GatewayDataProtection.Check(Options(null, KeyProtection.None), isDevelopment: false, isWindows: true);

        problems.Count.ShouldBe(2);
        problems.ShouldContain(p => p.Contains("KeysFolder", StringComparison.Ordinal));
        problems.ShouldContain(p => p.Contains("must be protected", StringComparison.Ordinal));
    }

    [Fact]
    public void A_certificate_needs_its_thumbprint_and_protection_needs_a_folder()
    {
        GatewayDataProtection.Check(Options(@"C:\keys", KeyProtection.Certificate), isDevelopment: false, isWindows: true)
            .ShouldHaveSingleItem().ShouldContain("CertificateThumbprint");
        GatewayDataProtection.Check(Options(null, KeyProtection.Certificate, "ABC123"), isDevelopment: true, isWindows: true)
            .ShouldHaveSingleItem().ShouldContain("KeysFolder");
    }
}
