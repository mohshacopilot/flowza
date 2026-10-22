using Flowza.Common.Hosting;
using Flowza.ServiceDefaults.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Flowza.Identity.Infrastructure.TokenServer;

/// <summary>
/// Client settings from configuration (<c>Identity:Clients</c>). Secrets come from user secrets in development and
/// from environment variables or a secret store elsewhere, never from git (constitution 7.6).
/// </summary>
public sealed class ClientRegistrationOptions
{
    public const string Section = "Identity:Clients";

    public GatewayClientOptions Gateway { get; set; } = new();

    public ServiceClientOptions Design { get; set; } = new();

    public ServiceClientOptions Runtime { get; set; } = new();

    public ServiceClientOptions Worker { get; set; } = new();
}

public class ServiceClientOptions
{
    public string? Secret { get; set; }
}

public sealed class GatewayClientOptions : ServiceClientOptions
{
    public IList<Uri> RedirectUris { get; } = [];

    public IList<Uri> PostLogoutRedirectUris { get; } = [];
}

/// <summary>Flowza's client IDs.</summary>
public static class FlowzaClients
{
    public const string Gateway = "flowza-gateway";
    public const string Design = "flowza-design";
    public const string Runtime = "flowza-runtime";
    public const string Worker = "flowza-worker";
}

/// <summary>
/// Creates or updates the OpenIddict client registrations at start-up: the Gateway (code flow + PKCE + refresh, SE1)
/// and one client per service (client credentials, SE3). A client without a configured secret is left out.
/// </summary>
internal sealed partial class ClientRegistrationSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<ClientRegistrationOptions> options,
    ILogger<ClientRegistrationSeeder> logger) : IStartupStep
{
    /// <summary>A start-up step (T034): when the database is down at start it runs once the database answers.</summary>
    public int Order => StartupStepOrder.Data;

    public string Name => "client registrations";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var settings = options.Value;

        await RegisterAsync(manager, GatewayDescriptor(settings.Gateway), cancellationToken);
        await RegisterAsync(manager, ServiceDescriptor(FlowzaClients.Design, settings.Design), cancellationToken);
        await RegisterAsync(manager, ServiceDescriptor(FlowzaClients.Runtime, settings.Runtime), cancellationToken);
        await RegisterAsync(manager, ServiceDescriptor(FlowzaClients.Worker, settings.Worker), cancellationToken);
    }

    private static OpenIddictApplicationDescriptor? GatewayDescriptor(GatewayClientOptions gateway)
    {
        if (string.IsNullOrWhiteSpace(gateway.Secret))
        {
            return null;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = FlowzaClients.Gateway,
            ClientSecret = gateway.Secret,
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "Flowza Gateway",
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
                Permissions.Prefixes.Scope + Scopes.OfflineAccess,
                Permissions.Prefixes.Scope + ApiSecurityExtensions.ApiScope,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };
        foreach (var uri in gateway.RedirectUris)
        {
            descriptor.RedirectUris.Add(uri);
        }

        foreach (var uri in gateway.PostLogoutRedirectUris)
        {
            descriptor.PostLogoutRedirectUris.Add(uri);
        }

        return descriptor;
    }

    private static OpenIddictApplicationDescriptor? ServiceDescriptor(string clientId, ServiceClientOptions service) =>
        string.IsNullOrWhiteSpace(service.Secret)
            ? null
            : new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                ClientSecret = service.Secret,
                ClientType = ClientTypes.Confidential,
                DisplayName = clientId,
                Permissions =
                {
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.ClientCredentials,
                    Permissions.Prefixes.Scope + ApiSecurityExtensions.ApiScope,
                },
            };

    private async Task RegisterAsync(IOpenIddictApplicationManager manager, OpenIddictApplicationDescriptor? descriptor, CancellationToken cancellationToken)
    {
        if (descriptor is null)
        {
            return;
        }

        var existing = await manager.FindByClientIdAsync(descriptor.ClientId!, cancellationToken);
        if (existing is null)
        {
            await manager.CreateAsync(descriptor, cancellationToken);
            LogRegistered(logger, descriptor.ClientId!);
        }
        else
        {
            // Settings may have changed (new secret, new addresses); the descriptor replaces them.
            await manager.UpdateAsync(existing, descriptor, cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered OpenID Connect client {ClientId}")]
    private static partial void LogRegistered(ILogger logger, string clientId);
}
