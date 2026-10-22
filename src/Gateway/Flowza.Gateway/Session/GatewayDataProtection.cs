using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;

namespace Flowza.Gateway.Session;

/// <summary>How the keys in <see cref="GatewayDataProtectionOptions.KeysFolder"/> are protected at rest.</summary>
public enum KeyProtection
{
    /// <summary>Not protected: development only.</summary>
    None,

    /// <summary>Encrypted with a certificate from the machine's or user's certificate store: one or several Gateways, Windows or Linux.</summary>
    Certificate,

    /// <summary>Windows DPAPI for the machine: one Gateway on one Windows server.</summary>
    Dpapi,
}

/// <summary>
/// Where the Gateway keeps the keys that encrypt its session cookie (task T030 item a; SE1). Outside Development the
/// keys must live in a folder that survives restarts and that every Gateway of the installation shares (a file share
/// with several Gateways), protected at rest. Otherwise a restart logs everyone out, and one Gateway cannot read
/// another's cookies. Azure (Blob Storage + Key Vault) comes with the cloud work and needs two packages the owner
/// approves then (owner, 2026-10-22).
/// </summary>
public sealed class GatewayDataProtectionOptions
{
    public const string Section = "Gateway:DataProtection";

    /// <summary>A local folder or a file share (UNC path); the Gateway's account needs read and write access.</summary>
    public string? KeysFolder { get; set; }

    public KeyProtection Protection { get; set; } = KeyProtection.None;

    /// <summary>For <see cref="KeyProtection.Certificate"/>: the certificate (with private key) in CurrentUser\My or LocalMachine\My.</summary>
    public string? CertificateThumbprint { get; set; }
}

public static class GatewayDataProtection
{
    /// <summary>Every Gateway of an installation uses the same name, so they share keys.</summary>
    public const string ApplicationName = "Flowza.Gateway";

    /// <summary>What is wrong with the settings; empty when the Gateway may start with them.</summary>
    public static IReadOnlyList<string> Check(GatewayDataProtectionOptions options, bool isDevelopment, bool isWindows)
    {
        ArgumentNullException.ThrowIfNull(options);
        var problems = new List<string>();
        var hasFolder = !string.IsNullOrWhiteSpace(options.KeysFolder);
        if (!isDevelopment && !hasFolder)
        {
            problems.Add($"'{GatewayDataProtectionOptions.Section}:KeysFolder' is not set: outside Development the Gateway keeps its session keys in a folder that survives restarts and that all Gateways share.");
        }

        if (!isDevelopment && options.Protection == KeyProtection.None)
        {
            problems.Add($"'{GatewayDataProtectionOptions.Section}:Protection' is None: outside Development the session keys must be protected (Certificate or Dpapi).");
        }

        if (options.Protection == KeyProtection.Certificate && string.IsNullOrWhiteSpace(options.CertificateThumbprint))
        {
            problems.Add($"'{GatewayDataProtectionOptions.Section}:CertificateThumbprint' is not set, but Protection is Certificate.");
        }

        if (options.Protection == KeyProtection.Dpapi && !isWindows)
        {
            problems.Add("Protection Dpapi works on Windows only; use Certificate.");
        }

        if (options.Protection != KeyProtection.None && !hasFolder)
        {
            problems.Add($"'{GatewayDataProtectionOptions.Section}:KeysFolder' is needed when the keys are protected.");
        }

        return problems;
    }

    public static TBuilder AddGatewayDataProtection<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        var options = builder.Configuration.GetSection(GatewayDataProtectionOptions.Section).Get<GatewayDataProtectionOptions>()
            ?? new GatewayDataProtectionOptions();
        var problems = Check(options, builder.Environment.IsDevelopment(), OperatingSystem.IsWindows());
        if (problems.Count > 0)
        {
            throw new InvalidOperationException("The Gateway's session keys are not set up: " + string.Join(" ", problems));
        }

        var dataProtection = builder.Services.AddDataProtection().SetApplicationName(ApplicationName);
        if (string.IsNullOrWhiteSpace(options.KeysFolder))
        {
            return builder; // Development: the default key store of the developer's own account.
        }

        dataProtection.PersistKeysToFileSystem(new DirectoryInfo(options.KeysFolder));
        if (options.Protection == KeyProtection.Certificate)
        {
            var certificate = FindCertificate(options.CertificateThumbprint!);
            dataProtection.ProtectKeysWithCertificate(certificate).UnprotectKeysWithAnyCertificate(certificate);
        }
        else if (options.Protection == KeyProtection.Dpapi && OperatingSystem.IsWindows())
        {
            dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
        }

        return builder;
    }

    /// <summary>
    /// The certificate with its private key, from CurrentUser\My or LocalMachine\My. Found by the Gateway itself and
    /// not by Data Protection's own lookup, which accepts only certificates trusted by the machine; installations often
    /// use a self-signed or internal certificate just for this. Missing: the Gateway stops at start-up, not at the
    /// first login.
    /// </summary>
    private static X509Certificate2 FindCertificate(string thumbprint)
    {
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
            $"The certificate '{thumbprint}' for the Gateway's session keys is not in CurrentUser\\My or LocalMachine\\My, or its private key is missing.");
    }
}
