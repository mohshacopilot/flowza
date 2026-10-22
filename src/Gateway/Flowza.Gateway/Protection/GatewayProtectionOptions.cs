namespace Flowza.Gateway.Protection;

/// <summary>
/// Limits of the Gateway's protection (tech discussion GW3, GW5, SE7), from configuration section
/// <c>Gateway:Protection</c>. The defaults are the M1 values.
/// </summary>
public sealed class GatewayProtectionOptions
{
    public const string Section = "Gateway:Protection";

    /// <summary>GW5: every request body except file uploads (feature 004) is at most 1 MB.</summary>
    public long MaxRequestBodyBytes { get; set; } = 1024 * 1024;

    /// <summary>L6 / IAM-07-AC2: password submissions per network address.</summary>
    public RateLimit LoginPerAddress { get; set; } = new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) };

    /// <summary>GW3: API calls per logged-in user (per network address when nobody is logged in).</summary>
    public RateLimit ApiPerUser { get; set; } = new() { PermitLimit = 600, Window = TimeSpan.FromMinutes(1) };

    /// <summary>SE7: the Content-Security-Policy; empty means the default for the environment (<see cref="SecurityHeaders"/>).</summary>
    public string? ContentSecurityPolicy { get; set; }
}

public sealed class RateLimit
{
    public int PermitLimit { get; set; }

    public TimeSpan Window { get; set; }
}
