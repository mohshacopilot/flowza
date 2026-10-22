using System.Security.Cryptography.X509Certificates;
using Flowza.Common.Hosting;
using Flowza.Identity.Infrastructure.AspNetIdentity;
using Flowza.Identity.Infrastructure.Persistence;
using Flowza.Messaging;
using Flowza.ServiceDefaults.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Flowza.Identity.Infrastructure.TokenServer;

/// <summary>
/// The OpenID Connect server of the Identity service (OpenIddict; tech discussion T5, SE1–SE3; plan 001 §5.4):
/// authorization code + PKCE for the Gateway (BFF), client credentials for the services, refresh tokens capped at
/// 12 hours (IAM-08, C1). Access tokens are signed but not encrypted, so every service checks them locally.
/// </summary>
public static class TokenServerExtensions
{
    public const string AuthorizationPath = "connect/authorize";
    public const string TokenPath = "connect/token";
    public const string EndSessionPath = "connect/logout";

    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>The absolute session limit (IAM-08: 12 hours); refresh tokens never extend it.</summary>
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromHours(12);

    public static TBuilder AddIdentityTokenServer<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        var configuration = builder.Configuration;
        var isDevelopment = builder.Environment.IsDevelopment();

        builder.Services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore()
                .UseDbContext<IdentityServiceDbContext>()
                .ReplaceDefaultEntities<Guid>())
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris(AuthorizationPath)
                    .SetTokenEndpointUris(TokenPath)
                    .SetEndSessionEndpointUris(EndSessionPath);

                options.AllowAuthorizationCodeFlow()
                    .RequireProofKeyForCodeExchange()
                    .AllowClientCredentialsFlow()
                    .AllowRefreshTokenFlow();

                options.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Roles, Scopes.OfflineAccess, ApiSecurityExtensions.ApiScope);

                options.SetAccessTokenLifetime(AccessTokenLifetime)
                    .SetRefreshTokenLifetime(RefreshTokenLifetime)
                    .DisableSlidingRefreshTokenExpiration();

                // Services validate access tokens with the signing keys only (SE3).
                options.DisableAccessTokenEncryption();

                if (configuration[IssuerKey] is { Length: > 0 } issuer)
                {
                    options.SetIssuer(new Uri(issuer));
                }

                AddKeys(options, configuration, isDevelopment);

                var aspNetCore = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();

                // HTTP only in development (owner decision SE7 amendment); HTTPS everywhere else.
                if (isDevelopment)
                {
                    aspNetCore.DisableTransportSecurityRequirement();
                }
            });

        builder.Services.Replace(ServiceDescriptor.Scoped<ISessionRevoker, OpenIddictSessionRevoker>());
        builder.Services.Configure<ClientRegistrationOptions>(configuration.GetSection(ClientRegistrationOptions.Section));
        builder.Services.AddSingleton<IStartupStep, ClientRegistrationSeeder>();
        return builder;
    }

    /// <summary>Optional fixed issuer; by default the issuer is the address Identity is called on.</summary>
    public const string IssuerKey = "Identity:Issuer";

    public const string SigningThumbprintKey = "Identity:Certificates:SigningThumbprint";
    public const string EncryptionThumbprintKey = "Identity:Certificates:EncryptionThumbprint";

    private static void AddKeys(OpenIddictServerBuilder options, IConfiguration configuration, bool isDevelopment)
    {
        if (isDevelopment)
        {
            // Self-signed certificates kept in the developer's certificate store (T013 "development signing keys").
            options.AddDevelopmentEncryptionCertificate().AddDevelopmentSigningCertificate();
            return;
        }

        if (ProductionDatabaseScripts.RequestedFolder(configuration) is not null)
        {
            // Only writing the database scripts (T033): nothing is signed, so the DBA needs no certificates for it.
            options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
            return;
        }

        // Production: certificates from the Windows certificate store, local machine (SE6); Key Vault later.
        var signing = configuration[SigningThumbprintKey];
        var encryption = configuration[EncryptionThumbprintKey];
        if (string.IsNullOrWhiteSpace(signing) || string.IsNullOrWhiteSpace(encryption))
        {
            throw new InvalidOperationException(
                $"Set '{SigningThumbprintKey}' and '{EncryptionThumbprintKey}' to certificates in the local machine store.");
        }

        options.AddSigningCertificate(signing, StoreName.My, StoreLocation.LocalMachine)
            .AddEncryptionCertificate(encryption, StoreName.My, StoreLocation.LocalMachine);
    }
}
