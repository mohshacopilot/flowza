using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Flowza.Gateway.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Flowza.Gateway.IntegrationTests.Production;

/// <summary>
/// The Gateway as on a real installation (environment Production; task T030): session keys in a folder protected
/// with a certificate, a trusted proxy in front, the built web app in wwwroot. No Identity behind it: these tests
/// look only at what the Gateway does itself.
/// </summary>
public sealed class ProductionGateway(IReadOnlyDictionary<string, string?> settings) : WebApplicationFactory<GatewayAssembly>
{
    /// <summary>The load balancer in front of the Gateway in these tests.</summary>
    public const string ProxyAddress = "10.9.9.9";

    public HttpClient CreateBrowser(string remoteAddress)
    {
        var browser = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("http://localhost/") });
        browser.DefaultRequestHeaders.Add(GatewayPlatform.TestRemoteAddress.HeaderName, remoteAddress);
        return browser;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Production");
        builder.UseSetting("Gateway:Oidc:ClientSecret", "test-gateway-secret");
        builder.UseSetting("Flowza:Identity:Authority", "https://identity.test/");
        builder.UseSetting("https_port", "443");
        // Production writes daily log files; keep them out of the project folder.
        builder.UseSetting("Flowza:Logging:Folder", Path.Combine(Path.GetTempPath(), "flowza-gw-test-logs"));
        // Identity is not there: forwarded requests fail at once instead of waiting for a name lookup.
        builder.UseSetting("ReverseProxy:Clusters:identity:Destinations:identity:Address", "http://127.0.0.1:9/");
        foreach (var (key, value) in settings)
        {
            if (key == "webroot")
            {
                builder.UseWebRoot(value!);
            }
            else
            {
                builder.UseSetting(key, value);
            }
        }

        builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, GatewayPlatform.TestRemoteAddress>());
    }
}

/// <summary>
/// A self-signed certificate in the current user's store for the tests' key protection, removed afterwards; a key
/// folder and a web root with a small built app, both temporary.
/// </summary>
public sealed class ProductionGatewayFiles : IDisposable
{
    public const string IndexMarker = "flowza-test-index";
    public const string AssetName = "index-AbC123.js";

    private readonly X509Certificate2 _certificate;
    private readonly List<string> _keyFolders = [];

    public ProductionGatewayFiles()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Flowza Gateway test keys", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        // Through PKCS#12 so the private key is stored with the certificate (Data Protection finds it in the store).
        _certificate = X509CertificateLoader.LoadPkcs12(
            created.Export(X509ContentType.Pkcs12),
            password: null,
            X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(_certificate);

        WebRoot = Directory.CreateTempSubdirectory("flowza-gw-web-").FullName;
        File.WriteAllText(Path.Combine(WebRoot, "index.html"), $"<!doctype html><html><body><div id=\"root\"></div><!-- {IndexMarker} --></body></html>");
        Directory.CreateDirectory(Path.Combine(WebRoot, "assets"));
        File.WriteAllText(Path.Combine(WebRoot, "assets", AssetName), "console.log('app');");
    }

    public string Thumbprint => _certificate.Thumbprint;

    public string WebRoot { get; }

    /// <summary>An empty key folder of its own (deleted with the fixture).</summary>
    public string NewKeysFolder()
    {
        var folder = Directory.CreateTempSubdirectory("flowza-gw-keys-").FullName;
        _keyFolders.Add(folder);
        return folder;
    }

    /// <summary>The settings of a correctly set-up installation; <paramref name="changes"/> override or remove (null) single ones.</summary>
    public Dictionary<string, string?> Settings(string keysFolder, params (string Key, string? Value)[] changes)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Gateway:DataProtection:KeysFolder"] = keysFolder,
            ["Gateway:DataProtection:Protection"] = "Certificate",
            ["Gateway:DataProtection:CertificateThumbprint"] = Thumbprint,
            ["ForwardedHeaders:KnownProxies:0"] = ProductionGateway.ProxyAddress,
            ["webroot"] = WebRoot,
        };
        foreach (var (key, value) in changes)
        {
            if (value is null)
            {
                settings.Remove(key);
            }
            else
            {
                settings[key] = value;
            }
        }

        return settings;
    }

    public void Dispose()
    {
        using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
        {
            store.Open(OpenFlags.ReadWrite);
            store.Remove(_certificate);
        }

        _certificate.Dispose();
        Directory.Delete(WebRoot, recursive: true);
        foreach (var folder in _keyFolders)
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProductionGatewayGroup : ICollectionFixture<ProductionGatewayFiles>
{
    public const string Name = "Production Gateway";
}
