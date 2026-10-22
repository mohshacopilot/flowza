using Flowza.Runtime.Infrastructure.Persistence;
using Flowza.Runtime.Infrastructure;
using Flowza.Messaging;
using Flowza.ServiceDefaults;
using Flowza.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowzaServiceDefaults();
builder.AddFlowzaApiSecurity();
builder.AddRuntimeInfrastructure();

var app = builder.Build();

// `--database-scripts <folder>`: write the production database scripts and stop (PLT-03-AC2, PL4; T033).
if (await app.TryWriteDatabaseScriptsAsync<RuntimeDbContext>("runtime"))
{
    return;
}

app.UseFlowzaErrorHandling();
app.UseAuthentication();
app.UseAuthorization();
app.MapFlowzaDefaultEndpoints();

// Small, login-protected endpoint that tells which service answered (used by Gateway and tests).
app.MapGet("/api/v1/info", () => new { service = "runtime" });


await app.RunAsync();