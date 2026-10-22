using System.Security.Cryptography;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

// Flowza local orchestration (PLT-02, task T006). Starts every service, the Gateway and smtp4dev on Windows
// without containers; SQL Server is the PC's own installation (tech discussion T9, DV1). Messages go through SQL Server
// (flowza_messaging), as on every on-premises installation (ADR-0005 amendment 2).
// Passwords come from the "flowza-dev" user secrets (docs/setup/set-secrets.ps1); other values from appsettings.json.
var builder = DistributedApplication.CreateBuilder(args);
var configuration = builder.Configuration;

string Required(string key) => configuration[key] is { Length: > 0 } value
    ? value
    : throw new InvalidOperationException(
        $"'{key}' is not set. Run docs\\setup\\set-secrets.ps1 (passwords) or check src\\AppHost\\Flowza.AppHost\\appsettings.json.");

// Connection strings are built with proper escaping (a password may contain ';', '@', '/' or ':') and handed
// to the services as secret values, so the dashboard masks them.
IResourceBuilder<ConnectionStringResource> SecretConnectionString(string name, Func<string> build)
{
    var value = builder.AddParameter($"{name}-value", build, secret: true);
    return builder.AddConnectionString(name, ReferenceExpression.Create($"{value}"));
}

IResourceBuilder<ConnectionStringResource> SqlDatabase(string name, string database) =>
    SecretConnectionString(name, () => new SqlConnectionStringBuilder
    {
        DataSource = Required("Sql:Server"),
        InitialCatalog = database,
        UserID = Required("Sql:User"),
        Password = Required("Sql:Password"),
        Encrypt = true,
        TrustServerCertificate = true,
    }.ConnectionString);

var identityDb = SqlDatabase("identity-db", "flowza_identity");
var designDb = SqlDatabase("design-db", "flowza_design");
var runtimeDb = SqlDatabase("runtime-db", "flowza_runtime");
var workerDb = SqlDatabase("worker-db", "flowza_worker");

// One transport for every service of the installation (ADR-0005): SqlServer on-premises, AzureServiceBus in the cloud.
// Only the selected transport's connection is created.
var transport = configuration["Messaging:Transport"] is { Length: > 0 } configured ? configured : "SqlServer";
IResourceBuilder<ConnectionStringResource> transportConnection = transport.ToUpperInvariant() switch
{
    "SQLSERVER" => SqlDatabase("messaging-db", "flowza_messaging"),
    "AZURESERVICEBUS" => SecretConnectionString("azure-servicebus", () => Required("AzureServiceBus:ConnectionString")),
    _ => throw new InvalidOperationException($"Messaging:Transport '{transport}' is not SqlServer or AzureServiceBus (ADR-0005)."),
};

IResourceBuilder<ProjectResource> Service<TProject>(string name, IResourceBuilder<ConnectionStringResource> database)
    where TProject : IProjectMetadata, new() =>
    builder.AddProject<TProject>(name)
        .WithReference(database)
        .WithReference(transportConnection)
        .WithEnvironment("Messaging__Transport", transport);

var identity = Service<Projects.Flowza_Identity_Api>("identity", identityDb);
var design = Service<Projects.Flowza_Design_Api>("design", designDb);
var runtime = Service<Projects.Flowza_Runtime_Api>("runtime", runtimeDb);
var worker = Service<Projects.Flowza_Worker>("worker", workerDb);

// The web app's Vite dev server (task T023), started as a program, not a container; "npm install" runs first.
// Fixed port 5105 next to the services' ports; the Gateway proxies every non-API path to it (T021).
var web = builder.AddViteApp("web", "../../Web")
    .WithNpm(install: true)
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5105;
        endpoint.IsProxied = false;
    });

// The Gateway logs users in as an OpenID Connect client of Identity (T020, SE1). Its client secret is new on every
// start, so no developer has to keep one; Identity updates the client registration at start-up.
var gatewaySecretValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var gatewaySecret = builder.AddParameter("gateway-client-secret", () => gatewaySecretValue, secret: true);

var gateway = builder.AddProject<Projects.Flowza_Gateway>("gateway")
    .WithReference(web)
    .WithReference(identity)
    .WithReference(design)
    .WithReference(runtime)
    .WithReference(worker)
    .WithEnvironment("Gateway__Oidc__ClientSecret", gatewaySecret)
    .WithEnvironment("Flowza__Identity__Authority", identity.GetEndpoint("http"))
    .WithExternalHttpEndpoints();

// The browser reaches Identity's pages through the Gateway, so tokens get one fixed issuer (Identity's own address,
// where services check them) and come back to the Gateway's addresses.
var gatewayEndpoint = gateway.GetEndpoint("http");
identity
    .WithEnvironment("Identity__Issuer", identity.GetEndpoint("http"))
    .WithEnvironment("Identity__Clients__Gateway__Secret", gatewaySecret)
    .WithEnvironment("Identity__Clients__Gateway__RedirectUris__0", ReferenceExpression.Create($"{gatewayEndpoint}/signin-oidc"))
    .WithEnvironment("Identity__Clients__Gateway__PostLogoutRedirectUris__0", ReferenceExpression.Create($"{gatewayEndpoint}/signout-callback-oidc"));

// Development mail catcher: every email the platform sends shows up at http://localhost:5000.
if (configuration.GetValue("Smtp4dev:Enabled", true) && FindSmtp4dev() is { } smtp4dev)
{
    builder.AddExecutable("smtp4dev", smtp4dev, ".", "--urls=http://localhost:5000", "--smtpport=2525")
        .WithHttpEndpoint(port: 5000, isProxied: false)
        .WithUrlForEndpoint("http", url => url.DisplayText = "Mailbox");
}

await builder.Build().RunAsync();

// smtp4dev is a .NET global tool; its launcher is a .cmd file, so start the real program it points to.
static string? FindSmtp4dev()
{
    var store = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", ".store", "rnwood.smtp4dev");
    return Directory.Exists(store)
        ? Directory.EnumerateFiles(store, "Rnwood.Smtp4dev.exe", SearchOption.AllDirectories).OrderDescending().FirstOrDefault()
        : null;
}
