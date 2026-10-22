using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowza.ServiceDefaults.Http;

/// <summary>
/// The proxies in front of a service whose <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> headers are believed
/// (task T030 item b; SE7): the load balancer in front of the Gateway (TLS ends there), and the Gateway in front of
/// Identity. Then HTTPS redirection and HSTS see the browser's https, Identity builds https addresses, and rate limits
/// see the client's address. Headers from any other sender are ignored, so a client cannot fake its address.
/// </summary>
public sealed class FlowzaForwardedHeadersOptions
{
    public const string Section = "ForwardedHeaders";

    /// <summary>Addresses of trusted proxies, e.g. "10.0.0.5".</summary>
    public IList<string> KnownProxies { get; } = [];

    /// <summary>Networks of trusted proxies, e.g. "10.0.0.0/24".</summary>
    public IList<string> KnownNetworks { get; } = [];

    /// <summary>How many proxies in a row are trusted (load balancer → Gateway → Identity is 2 for Identity).</summary>
    public int ForwardLimit { get; set; } = 1;

    public bool IsConfigured => KnownProxies.Count > 0 || KnownNetworks.Count > 0;
}

public static class FlowzaForwardedHeadersExtensions
{
    /// <summary>Reads the trusted proxies; nothing is trusted when none are configured. A malformed entry stops the start.</summary>
    public static TBuilder AddFlowzaForwardedHeaders<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        var settings = Read(builder.Configuration);
        if (!settings.IsConfigured)
        {
            return builder;
        }

        var proxies = settings.KnownProxies.Select(value => IPAddress.TryParse(value, out var address)
            ? address
            : throw new InvalidOperationException($"'{FlowzaForwardedHeadersOptions.Section}:KnownProxies' has an entry that is not an IP address."))
            .ToList();
        var networks = settings.KnownNetworks.Select(value => System.Net.IPNetwork.TryParse(value, out var network)
            ? network
            : throw new InvalidOperationException($"'{FlowzaForwardedHeadersOptions.Section}:KnownNetworks' has an entry that is not a network like 10.0.0.0/24."))
            .ToList();

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = settings.ForwardLimit;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(proxy);
            }

            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(network);
            }
        });
        return builder;
    }

    /// <summary>Must be the first middleware, before HTTPS redirection, HSTS and anything that reads the address.</summary>
    public static WebApplication UseFlowzaForwardedHeaders(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (Read(app.Configuration).IsConfigured)
        {
            app.UseForwardedHeaders();
        }

        return app;
    }

    private static FlowzaForwardedHeadersOptions Read(IConfiguration configuration) =>
        configuration.GetSection(FlowzaForwardedHeadersOptions.Section).Get<FlowzaForwardedHeadersOptions>()
        ?? new FlowzaForwardedHeadersOptions();
}
