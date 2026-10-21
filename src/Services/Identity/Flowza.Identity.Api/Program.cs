using Flowza.Identity.Api;
using Flowza.Identity.Api.Connect;
using Flowza.Identity.Infrastructure;
using Flowza.Identity.Infrastructure.Persistence;
using Flowza.Identity.Infrastructure.TokenServer;
using Flowza.Persistence;
using Flowza.ServiceDefaults;
using Flowza.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowzaServiceDefaults();
builder.AddIdentityInfrastructure();
builder.AddIdentityTokenServer();

// Identity checks its own access tokens against its local token server, including whether they were revoked (K7).
builder.AddFlowzaApiSecurity(validation =>
{
    validation.UseLocalServer();
    validation.EnableTokenEntryValidation();
});
builder.AddIdentityLoginSession();

var app = builder.Build();

app.UseFlowzaErrorHandling();
app.UseIdentityLoginPages();
app.UseAuthentication();
app.UseAuthorization();
app.MapFlowzaDefaultEndpoints();
app.MapConnectEndpoints();
app.MapRazorPages();

// Small, login-protected endpoint that tells which service answered (used by Gateway and tests).
app.MapGet("/api/v1/info", () => new { service = "identity" });

if (app.Environment.IsDevelopment())
{
    // Development only: production applies reviewed SQL scripts (PLT-03, PL4).
    await app.Services.MigrateDatabaseAsync<IdentityServiceDbContext>();
}

await app.RunAsync();
