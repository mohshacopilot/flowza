using Flowza.Identity.Infrastructure;
using Flowza.Identity.IntegrationTests.Infrastructure;
using Flowza.Testing;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowza.Identity.IntegrationTests.Cli;

/// <summary>
/// Phase 3–4 review finding 8: outside Development Identity's own Data Protection keys (login cookie, anti-forgery)
/// are stored encrypted with its token encryption certificate, so reading <c>flowza_identity</c> is not enough to forge
/// a login. Development keeps them readable, as before.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class LoginKeyProtectionTests(IdentityApp app)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Outside_development_identitys_login_keys_are_stored_encrypted_with_its_certificate()
    {
        using var certificate = new TestCertificate("Flowza Identity test encryption");
        var before = await KeyIdsAsync();
        try
        {
            using var host = BuildIdentity("Production", certificate.Thumbprint);
            // A new key, as Data Protection makes one every 90 days (the database already holds a valid one).
            host.Services.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90));

            var created = await NewKeysAsync(before);
            created.ShouldNotBeEmpty();
            created.ShouldAllBe(xml => xml.Contains("encryptedSecret") && !xml.Contains("<masterKey"));
        }
        finally
        {
            await DeleteNewKeysAsync(before);
        }
    }

    [Fact]
    public void Outside_development_identity_refuses_to_start_without_its_encryption_certificate()
    {
        var error = Should.Throw<InvalidOperationException>(() => BuildIdentity("Production", thumbprint: null));

        error.Message.ShouldContain("Identity:Certificates:EncryptionThumbprint");
    }

    /// <summary>Identity's infrastructure as a Production installation builds it (not started).</summary>
    private static IHost BuildIdentity(string environment, string? thumbprint)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:identity-db"] = TestSqlServer.ConnectionString(IdentityApp.Database),
            ["ConnectionStrings:messaging-db"] = TestSqlServer.ConnectionString("flowza_test_messaging"),
            ["Identity:Certificates:EncryptionThumbprint"] = thumbprint,
        });
        builder.AddIdentityInfrastructure();
        builder.AddIdentityLoginKeyProtection();
        return builder.Build();
    }

    private Task<List<int>> KeyIdsAsync() =>
        app.WithDbContextAsync(context => context.DataProtectionKeys.Select(k => k.Id).ToListAsync(Ct));

    private async Task<List<string>> NewKeysAsync(List<int> before) =>
        await app.WithDbContextAsync(context => context.DataProtectionKeys
            .Where(k => !before.Contains(k.Id)).Select(k => k.Xml).ToListAsync(Ct));

    /// <summary>The test's key is encrypted for a certificate that is removed afterwards; the shared database must not keep it.</summary>
    private async Task DeleteNewKeysAsync(List<int> before) =>
        await app.WithDbContextAsync(context => context.DataProtectionKeys.Where(k => !before.Contains(k.Id)).ExecuteDeleteAsync(Ct));
}
