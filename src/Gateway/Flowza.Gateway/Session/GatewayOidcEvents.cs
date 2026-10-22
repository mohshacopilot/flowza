using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.Extensions;

namespace Flowza.Gateway.Session;

/// <summary>
/// The browser reaches Identity's login and logout pages through the Gateway (GW1: <c>/connect/**</c> and
/// <c>/account/**</c>), so the app has one address and Identity need not be public. Only the Gateway's back channel
/// (discovery, code exchange, refresh) calls Identity directly.
/// </summary>
internal sealed class GatewayOidcEvents(TimeProvider time) : OpenIdConnectEvents
{
    public override Task RedirectToIdentityProvider(RedirectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.ProtocolMessage.IssuerAddress = ThroughGateway(context.Request, context.ProtocolMessage.IssuerAddress);
        return base.RedirectToIdentityProvider(context);
    }

    public override Task RedirectToIdentityProviderForSignOut(RedirectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.ProtocolMessage.IssuerAddress = ThroughGateway(context.Request, context.ProtocolMessage.IssuerAddress);
        return base.RedirectToIdentityProviderForSignOut(context);
    }

    public override Task TicketReceived(TicketReceivedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Properties ??= new AuthenticationProperties();

        // A browser-session cookie; the login time starts the 12-hour limit (IAM-08).
        context.Properties.IsPersistent = false;
        SessionRules.MarkLogin(context.Properties, time.GetUtcNow());
        return base.TicketReceived(context);
    }

    /// <summary>Same path as Identity's endpoint (e.g. <c>/connect/authorize</c>), on the Gateway's own origin.</summary>
    internal static string ThroughGateway(HttpRequest request, string identityAddress) =>
        UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, new PathString(new Uri(identityAddress).AbsolutePath));
}
