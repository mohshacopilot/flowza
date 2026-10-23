using Flowza.ServiceDefaults.Security;
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
            dataProtection.ProtectKeysWithStoreCertificate(
                KeyCertificates.Find(options.CertificateThumbprint!, GatewayDataProtectionOptions.Section + ":CertificateThumbprint"));
        }
        else if (options.Protection == KeyProtection.Dpapi && OperatingSystem.IsWindows())
        {
            dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
        }

        return builder;
    }
}
