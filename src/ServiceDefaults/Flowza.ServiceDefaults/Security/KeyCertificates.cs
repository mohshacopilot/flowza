using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;

namespace Flowza.ServiceDefaults.Security;

/// <summary>
/// Certificates that protect a service's Data Protection keys at rest (the keys that encrypt cookies and tokens):
/// the Gateway's session keys (T030) and Identity's login keys (phase 3–4 review finding 8). New keys are encrypted
/// with the configured certificate. Reading a key finds its certificate in the machine's store by itself, so after a
/// certificate renewal older keys stay readable as long as the old certificate stays installed (finding 6; the
/// setup guide says so).
/// </summary>
public static class KeyCertificates
{
    /// <summary>
    /// The certificate with its private key, from CurrentUser\My or LocalMachine\My. Found here and not by Data
    /// Protection's own lookup, which accepts only certificates the machine trusts; installations often use a
    /// self-signed or internal certificate just for this. Missing: the service stops at start-up, naming the setting.
    /// </summary>
    public static X509Certificate2 Find(string thumbprint, string setting)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);
        foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            using var store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly);
            var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false)
                .FirstOrDefault(certificate => certificate.HasPrivateKey);
            if (found is not null)
            {
                return found;
            }
        }

        throw new InvalidOperationException(
            $"The certificate '{thumbprint}' ({setting}) is not in CurrentUser\\My or LocalMachine\\My, or its private key is missing.");
    }

    /// <summary>New keys are encrypted with <paramref name="certificate"/>, which can also read them.</summary>
    public static IDataProtectionBuilder ProtectKeysWithStoreCertificate(this IDataProtectionBuilder builder, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(certificate);
        return builder.ProtectKeysWithCertificate(certificate).UnprotectKeysWithAnyCertificate(certificate);
    }
}
