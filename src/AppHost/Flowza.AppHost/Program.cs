using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

// Flowza local orchestration (PLT-02, task T006). Starts every service, the Gateway and smtp4dev on Windows
// without containers; SQL Server and RabbitMQ are the PC's own installations (tech discussion T9, DV1).
// Passwords come from the "flowza-dev" user secrets (docs/setup/set-secrets.ps1); other values from appsettings.json.
var builder = DistributedApplication.CreateBuilder(args);
var configuration = builder.Configuration;

string Required(string key) => configuration[key] is { Length: > 0 } value
    ? value
    : throw new InvalidOperationException(
        $"'{key}' is not set. Run docs\\setup\\set-secrets.ps1 (passwords) or check src\\AppHost\\Flowza.AppHost\\appsettings.json.");

var sqlPassword = builder.AddParameter("sql-password", () => Required("Sql:Password"), secret: true);
var rabbitPassword = builder.AddParameter("rabbitmq-password", () => Required("RabbitMq:Password"), secret: true);

var sqlServer = Required("Sql:Server");
var sqlUser = Required("Sql:User");

IResourceBuilder<ConnectionStringResource> SqlDatabase(string name, string database) =>
    builder.AddConnectionString(name, ReferenceExpression.Create(
        $"Server={sqlServer};Database={database};User ID={sqlUser};Password={sqlPassword};Encrypt=True;TrustServerCertificate=True"));

var identityDb = SqlDatabase("identity-db", "flowza_identity");
var designDb = SqlDatabase("design-db", "flowza_design");
var runtimeDb = SqlDatabase("runtime-db", "flowza_runtime");
var workerDb = SqlDatabase("worker-db", "flowza_worker");
var messagingDb = SqlDatabase("messaging-db", "flowza_messaging");

var rabbitHost = Required("RabbitMq:Host");
var rabbitPort = Required("RabbitMq:Port");
var rabbitUser = Required("RabbitMq:User");
var rabbitVirtualHost = Required("RabbitMq:VirtualHost");
var rabbitMq = builder.AddConnectionString("rabbitmq", ReferenceExpression.Create(
    $"amqp://{rabbitUser}:{rabbitPassword}@{rabbitHost}:{rabbitPort}/{rabbitVirtualHost}"));

// One transport value for every service of the installation (ADR-0005).
var transport = configuration["Messaging:Transport"] ?? "SqlServer";

IResourceBuilder<ProjectResource> Service<TProject>(string name, IResourceBuilder<ConnectionStringResource> database)
    where TProject : IProjectMetadata, new() =>
    builder.AddProject<TProject>(name)
        .WithReference(database)
        .WithReference(messagingDb)
        .WithReference(rabbitMq)
        .WithEnvironment("Messaging__Transport", transport);

var identity = Service<Projects.Flowza_Identity_Api>("identity", identityDb);
var design = Service<Projects.Flowza_Design_Api>("design", designDb);
var runtime = Service<Projects.Flowza_Runtime_Api>("runtime", runtimeDb);
var worker = Service<Projects.Flowza_Worker>("worker", workerDb);

builder.AddProject<Projects.Flowza_Gateway>("gateway")
    .WithReference(identity)
    .WithReference(design)
    .WithReference(runtime)
    .WithReference(worker)
    .WithExternalHttpEndpoints();

// Development mail catcher: every email the platform sends shows up at http://localhost:5000.
if (configuration.GetValue("Smtp4dev:Enabled", true) && FindSmtp4dev() is { } smtp4dev)
{
    builder.AddExecutable("smtp4dev", smtp4dev, ".", "--urls=http://localhost:5000", "--smtpport=2525")
        .WithHttpEndpoint(port: 5000, isProxied: false)
        .WithUrlForEndpoint("http", url => url.DisplayText = "Mailbox");
}

// The web app (Vite) joins in task T023.
await builder.Build().RunAsync();

// smtp4dev is a .NET global tool; its launcher is a .cmd file, so start the real program it points to.
static string? FindSmtp4dev()
{
    var store = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", ".store", "rnwood.smtp4dev");
    return Directory.Exists(store)
        ? Directory.EnumerateFiles(store, "Rnwood.Smtp4dev.exe", SearchOption.AllDirectories).OrderDescending().FirstOrDefault()
        : null;
}
