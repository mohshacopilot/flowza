using Flowza.Runtime.Infrastructure.Persistence;
using Flowza.Runtime.Infrastructure;
using Flowza.Persistence;
using Flowza.ServiceDefaults;
using Flowza.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowzaServiceDefaults();
builder.AddFlowzaApiSecurity();
builder.AddRuntimePersistence();

var app = builder.Build();

app.UseFlowzaErrorHandling();
app.UseAuthentication();
app.UseAuthorization();
app.MapFlowzaDefaultEndpoints();

// Small, login-protected endpoint that tells which service answered (used by Gateway and tests).
app.MapGet("/api/v1/info", () => new { service = "runtime" });

if (app.Environment.IsDevelopment())
{
    // Development only: production applies reviewed SQL scripts (PLT-03, PL4).
    await app.Services.MigrateDatabaseAsync<RuntimeDbContext>();
}

await app.RunAsync();