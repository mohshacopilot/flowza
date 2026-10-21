using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Validation;
using OpenIddict.Validation.AspNetCore;

namespace Flowza.ServiceDefaults.Security;

/// <summary>
/// Every service API requires a valid access token from the Identity service, except health checks (IAM-05;
/// constitution 7.1). Tokens are validated locally with Identity's signing keys (tech discussion T5, SE3); roles are
/// checked by the policies below and records in the handlers (SE4).
/// </summary>
public static class ApiSecurityExtensions
{
    /// <summary>Address of the Identity service (token issuer); required outside Development.</summary>
    public const string AuthorityKey = "Flowza:Identity:Authority";

    /// <summary>Identity's fixed development address (launch profile), used when <see cref="AuthorityKey"/> is not set.</summary>
    public const string DevelopmentAuthority = "http://localhost:5101/";

    /// <summary>Audience of Flowza access tokens; every service accepts it.</summary>
    public const string Audience = "flowza";

    /// <summary>Scope that lets a client call the Flowza APIs.</summary>
    public const string ApiScope = "flowza_api";

    public static TBuilder AddFlowzaApiSecurity<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder =>
        builder.AddFlowzaApiSecurity(configureValidation: null);

    /// <param name="builder">The host builder.</param>
    /// <param name="configureValidation">
    /// Where the signing keys come from. Default: Identity's discovery document at <see cref="AuthorityKey"/>. The Identity
    /// service itself passes its local token server.
    /// </param>
    public static TBuilder AddFlowzaApiSecurity<TBuilder>(this TBuilder builder, Action<OpenIddictValidationBuilder>? configureValidation)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOpenIddict().AddValidation(options =>
        {
            if (configureValidation is null)
            {
                options.SetIssuer(ReadAuthority(builder.Configuration, builder.Environment));
                options.UseSystemNetHttp();
            }
            else
            {
                configureValidation(options);
            }

            options.AddAudiences(Audience);
            options.UseAspNetCore();
        });

        builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(FlowzaPolicies.PlatformAdmin, policy => policy.RequireAuthenticatedUser().RequireRole(FlowzaRoles.PlatformAdmin))
            .AddPolicy(FlowzaPolicies.AppDesigner, policy => policy.RequireAuthenticatedUser().RequireRole(FlowzaRoles.AppDesigner));

        return builder;
    }

    public static Uri ReadAuthority(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        var value = configuration[AuthorityKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return environment.IsDevelopment()
                ? new Uri(DevelopmentAuthority)
                : throw new InvalidOperationException($"'{AuthorityKey}' is not set: services need the Identity service's address to check tokens.");
        }

        return new Uri(value.EndsWith('/') ? value : value + "/");
    }
}

/// <summary>System role names as they appear in tokens (IAM-02; same names as the Identity domain's SystemRole).</summary>
public static class FlowzaRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string AppDesigner = "AppDesigner";
    public const string User = "User";
}

/// <summary>Authorization policies for endpoints (SE4): designer APIs need App Designer, directory APIs Platform Admin (IAM-02-AC1).</summary>
public static class FlowzaPolicies
{
    public const string PlatformAdmin = nameof(PlatformAdmin);
    public const string AppDesigner = nameof(AppDesigner);
}
