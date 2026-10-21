using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flowza.ServiceDefaults.Security;

/// <summary>
/// Every service API requires a logged-in caller, except health checks (IAM-05; constitution 7.1). Until the token
/// validation of task T013 is plugged in, no caller can authenticate, so every API call is answered with 401.
/// </summary>
public static class ApiSecurityExtensions
{
    public const string PendingScheme = "FlowzaPending";

    public static TBuilder AddFlowzaApiSecurity<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddAuthentication(PendingScheme)
            .AddScheme<AuthenticationSchemeOptions, NoCallerAuthenticationHandler>(PendingScheme, configureOptions: null);

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return builder;
    }

    /// <summary>Authenticates nobody; replaced by token validation in task T013.</summary>
    private sealed class NoCallerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());
    }
}
