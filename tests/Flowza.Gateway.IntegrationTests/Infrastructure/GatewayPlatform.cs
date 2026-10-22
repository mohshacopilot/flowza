using System.Collections.Concurrent;
using System.Net;
using Flowza.Gateway;
using Flowza.Gateway.Session;
using Flowza.Identity.Api;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Users;
using Flowza.Testing;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;

namespace Flowza.Gateway.IntegrationTests.Infrastructure;

/// <summary>
/// Identity (on <c>flowza_test_identity</c>) and the Gateway, both in memory and wired together: YARP and the
/// Gateway's back channel reach Identity's test server; the browser talks only to the Gateway. Both answer at
/// <c>http://localhost/</c>, so Identity's fixed issuer and the Gateway's addresses line up as on a real installation.
/// The Gateway's clock is a <see cref="ManualTimeProvider"/>, so tests can let session time pass.
/// </summary>
public sealed class GatewayPlatform : IAsyncLifetime
{
    public const string Database = "flowza_test_identity";
    public const string Origin = "http://localhost/";
    public const string ClientSecret = "test-gateway-secret";
    public const string Password = "Correct-Horse-42";

    private TestDatabaseLock? _lock;

    public IdentityHost Identity { get; } = new();

    public GatewayHost Gateway { get; private set; } = null!;

    public ManualTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>Counts the Gateway's calls to Identity's token endpoint (code exchange and refreshes).</summary>
    public CountingHandler TokenCalls { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _lock = await TestDatabaseLock.AcquireAsync(Database, TestContext.Current.CancellationToken);
        _ = Identity.Services; // starts Identity; it migrates its database in Development
        TokenCalls = new CountingHandler(Identity.Server.CreateHandler(), "/connect/token");
        Gateway = new GatewayHost(this);
        _ = Gateway.Services;
    }

    /// <summary>What the Gateway forwarded to Identity (through YARP), newest last.</summary>
    public RecordingHandler Forwarded { get; } = new();

    /// <summary>
    /// A browser: keeps cookies, does not follow redirects (tests follow them step by step). Each browser has its own
    /// network address, so per-address rate limits of one test never touch another.
    /// </summary>
    public HttpClient CreateBrowser(string? remoteAddress = null)
    {
        var browser = Gateway.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true, BaseAddress = new Uri(Origin) });
        browser.DefaultRequestHeaders.Add(TestRemoteAddress.HeaderName, remoteAddress ?? $"10.0.{Random.Shared.Next(256)}.{Random.Shared.Next(1, 255)}");
        return browser;
    }

    /// <summary>API calls per user per minute in the test Gateway (the real default is 600).</summary>
    public const int ApiPermitLimit = 50;

    /// <summary>A new active user with a known password; usernames are unique, so tests need no data reset.</summary>
    public async Task<User> CreateUserAsync(params SystemRole[] roles)
    {
        await using var scope = Identity.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var username = $"gw{Guid.NewGuid():N}"[..14];
        var user = User.Create(Guid.NewGuid(), username, $"Test {username}", $"{username}@example.org", null, roles).Value;
        services.GetRequiredService<IUserRepository>().Add(user);
        (await services.GetRequiredService<IUserCredentials>().SetPasswordAsync(user, Password, default)).IsSuccess.ShouldBeTrue();
        user.PasswordChanged(DateTimeOffset.UtcNow);
        (await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default)).IsSuccess.ShouldBeTrue();
        return user;
    }

    public async ValueTask DisposeAsync()
    {
        if (Gateway is not null)
        {
            await Gateway.DisposeAsync();
        }

        await Identity.DisposeAsync();
        if (_lock is not null)
        {
            await _lock.DisposeAsync();
        }
    }

    public sealed class IdentityHost : WebApplicationFactory<ApiAssembly>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // Program reads its connection strings while the host is being built, so they arrive as environment variables.
            Environment.SetEnvironmentVariable("ConnectionStrings__identity-db", TestSqlServer.ConnectionString(Database));
            Environment.SetEnvironmentVariable("ConnectionStrings__messaging-db", TestSqlServer.ConnectionString("flowza_test_messaging"));
            builder.UseEnvironment("Development");
            builder.UseSetting("Identity:Issuer", Origin);
            builder.UseSetting("Identity:Clients:Gateway:Secret", ClientSecret);
            builder.UseSetting("Identity:Clients:Gateway:RedirectUris:0", Origin + "signin-oidc");
            builder.UseSetting("Identity:Clients:Gateway:PostLogoutRedirectUris:0", Origin + "signout-callback-oidc");
        }
    }

    public sealed class GatewayHost(GatewayPlatform platform) : WebApplicationFactory<GatewayAssembly>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.UseEnvironment("Development");
            builder.UseSetting("Gateway:Oidc:ClientSecret", ClientSecret);
            builder.UseSetting("Flowza:Identity:Authority", Origin);
            builder.UseSetting("ReverseProxy:Clusters:identity:Destinations:identity:Address", Origin);
            builder.UseSetting("Gateway:Protection:ApiPerUser:PermitLimit", ApiPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IStartupFilter, TestRemoteAddress>();
                services.AddSingleton<TimeProvider>(platform.Clock);
                services.AddSingleton<IForwarderHttpClientFactory>(new TestServerForwarderFactory(platform));
                services.Configure<OpenIdConnectOptions>(GatewaySession.OidcScheme, options => options.BackchannelHttpHandler = platform.TokenCalls);
            });
        }
    }

    /// <summary>YARP forwards to Identity's in-memory server instead of the network, through the recorder.</summary>
    private sealed class TestServerForwarderFactory(GatewayPlatform platform) : IForwarderHttpClientFactory
    {
        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
        {
            platform.Forwarded.InnerHandler ??= platform.Identity.Server.CreateHandler();
            return new HttpMessageInvoker(platform.Forwarded, disposeHandler: false);
        }
    }

    /// <summary>TestServer has no client address; tests set one per browser with a header (test-only middleware).</summary>
    internal sealed class TestRemoteAddress : IStartupFilter
    {
        public const string HeaderName = "X-Test-Remote-Address";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (IPAddress.TryParse(context.Request.Headers[HeaderName], out var address))
                {
                    context.Connection.RemoteIpAddress = address;
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }
}

/// <summary>Passes requests on and keeps the path and headers of each, as the receiving service saw them.</summary>
public sealed class RecordingHandler : DelegatingHandler
{
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();

    public IReadOnlyList<RecordedRequest> Requests => [.. _requests];

    public RecordedRequest Last(string path) => Requests.Last(r => r.Path == path);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value), StringComparer.OrdinalIgnoreCase);
        _requests.Enqueue(new RecordedRequest(request.Method.Method, request.RequestUri!.AbsolutePath, headers));
        return base.SendAsync(request, cancellationToken);
    }
}

public sealed record RecordedRequest(string Method, string Path, IReadOnlyDictionary<string, string> Headers);

/// <summary>Passes requests on and counts those to one path.</summary>
public sealed class CountingHandler(HttpMessageHandler inner, string path) : DelegatingHandler(inner)
{
    private int _count;

    public int Count => _count;

    /// <summary>When set, requests to the path get this answer instead of reaching Identity (Identity "down").</summary>
    public HttpStatusCode? FailWith { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsolutePath == path)
        {
            Interlocked.Increment(ref _count);
            if (FailWith is { } status)
            {
                return Task.FromResult(new HttpResponseMessage(status) { RequestMessage = request });
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>All Gateway integration tests share one platform and run one after another.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GatewayTestGroup : ICollectionFixture<GatewayPlatform>
{
    public const string Name = "Gateway";
}
