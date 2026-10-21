using Flowza.Identity.Infrastructure.Persistence;
using Flowza.Identity.Infrastructure;
using Flowza.Persistence;
using Flowza.ServiceDefaults;
using Flowza.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowzaServiceDefaults();
builder.AddFlowzaApiSecurity();
builder.AddIdentityPersistence();

var app = builder.Build();

app.UseFlowzaErrorHandling();
app.UseAuthentication();
app.UseAuthorization();
app.MapFlowzaDefaultEndpoints();

// Small, login-protected endpoint that tells which service answered (used by Gateway and tests).
app.MapGet("/api/v1/info", () => new { service = "identity" });

if (app.Environment.IsDevelopment())
{
    // Development only: production applies reviewed SQL scripts (PLT-03, PL4).
    await app.Services.MigrateDatabaseAsync<IdentityServiceDbContext>();
}

await app.RunAsync();