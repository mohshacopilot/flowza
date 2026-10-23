using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Flowza.Testing;

/// <summary>
/// A self-signed certificate with private key in the current user's store (CurrentUser\My) for as long as this object
/// lives, for tests of key protection (T030, phase 3–4 review findings 6 and 8). Removed again on dispose.
/// </summary>
public sealed class TestCertificate : IDisposable
{
    private readonly X509Certificate2 _certificate;

    public TestCertificate(string name = "Flowza test keys")
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        // Through PKCS#12 so the private key is stored with the certificate (found again in the store).
        _certificate = X509CertificateLoader.LoadPkcs12(
            created.Export(X509ContentType.Pkcs12),
            password: null,
            X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(_certificate);
    }

    public string Thumbprint => _certificate.Thumbprint;

    public void Dispose()
    {
        using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
        {
            store.Open(OpenFlags.ReadWrite);
            store.Remove(_certificate);
        }

        _certificate.Dispose();
    }
}
