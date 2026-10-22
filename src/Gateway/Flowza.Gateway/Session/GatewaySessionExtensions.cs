using Flowza.ServiceDefaults.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Flowza.Gateway.Session;

/// <summary>The Gateway's login as an OpenID Connect client of Identity (code flow + PKCE, SE1) and its session cookie.</summary>
public sealed class GatewayOidcOptions
{
    public const string Section = "Gateway:Oidc";

    public string ClientId { get; set; } = "flowza-gateway";

    /// <summary>From the AppHost in development, from the environment or a secret store elsewhere; never in git.</summary>
    public string? ClientSecret { get; set; }
}

public static class GatewaySessionExtensions
{
    public static TBuilder AddGatewaySession<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        var isDevelopment = builder.Environment.IsDevelopment();
        var client = builder.Configuration.GetSection(GatewayOidcOptions.Section).Get<GatewayOidcOptions>() ?? new GatewayOidcOptions();
        if (string.IsNullOrWhiteSpace(client.ClientSecret))
        {
            throw new InvalidOperationException(
                $"'{GatewayOidcOptions.Section}:ClientSecret' is not set: the Gateway needs it to log users in through Identity (the AppHost sets it in development).");
        }

        var authority = ApiSecurityExtensions.ReadAuthority(builder.Configuration, builder.Environment);

        // HTTP only in development (owner decision SE7 amendment); Secure cookies everywhere else.
        var securePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

        // The session cookie is encrypted with Data Protection; the keys stay with the Gateway (no Gateway database).
        builder.Services.AddDataProtection().SetApplicationName("Flowza.Gateway");

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<OidcTokenRefresher>();
        builder.Services.AddSingleton<ITokenRefresher>(services => new SingleFlightTokenRefresher(
            services.GetRequiredService<OidcTokenRefresher>(),
            services.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton<SessionCookieEvents>();
        builder.Services.AddSingleton<GatewayOidcEvents>();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = GatewaySession.CookieScheme;
                options.DefaultChallengeScheme = GatewaySession.CookieScheme;
            })
            .AddCookie(GatewaySession.CookieScheme, options =>
            {
                options.Cookie.Name = GatewaySession.CookieName(isDevelopment);
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = securePolicy;
                options.ExpireTimeSpan = GatewaySession.IdleTimeout;
                options.SlidingExpiration = true;
                options.EventsType = typeof(SessionCookieEvents);
            })
            .AddOpenIdConnect(GatewaySession.OidcScheme, options =>
            {
                options.SignInScheme = GatewaySession.CookieScheme;
                options.Authority = authority.ToString();
                options.RequireHttpsMetadata = !isDevelopment;
                options.ClientId = client.ClientId;
                options.ClientSecret = client.ClientSecret;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.ResponseMode = OpenIdConnectResponseMode.Query;
                options.UsePkce = true;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = false;
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = "name";
                options.TokenValidationParameters.RoleClaimType = "role";
                options.Scope.Clear();
                foreach (var scope in GatewaySession.Scopes)
                {
                    options.Scope.Add(scope);
                }

                // The login round trip stays on the Gateway's origin, so Lax is enough for these short-lived cookies.
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.CorrelationCookie.SecurePolicy = securePolicy;
                options.NonceCookie.SameSite = SameSiteMode.Lax;
                options.NonceCookie.SecurePolicy = securePolicy;
                options.EventsType = typeof(GatewayOidcEvents);
            });

        // Both handlers read "now" from the same clock as the session rules (tests replace it).
        builder.Services.AddOptions<CookieAuthenticationOptions>(GatewaySession.CookieScheme)
            .Configure<TimeProvider>((options, clock) => options.TimeProvider = clock);
        builder.Services.AddOptions<OpenIdConnectOptions>(GatewaySession.OidcScheme)
            .Configure<TimeProvider>((options, clock) => options.TimeProvider = clock);

        builder.Services.AddAuthorization();
        return builder;
    }
}
