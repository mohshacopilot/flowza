using Flowza.Identity.Api;
using Flowza.Identity.Api.Connect;
using Flowza.Identity.Api.Installation;
using Flowza.Identity.Api.Me;
using Flowza.Identity.Api.Organization;
using Flowza.Identity.Api.Users;
using Flowza.Identity.Infrastructure;
using Flowza.Identity.Infrastructure.Installation;
using Flowza.Identity.Infrastructure.Persistence;
using Flowza.Identity.Infrastructure.TokenServer;
using Flowza.Messaging;
using Flowza.ServiceDefaults;
using Flowza.ServiceDefaults.Http;
using Flowza.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowzaServiceDefaults();
// Behind the Gateway (and a load balancer): trust their X-Forwarded headers, so Identity sees https (T030 item b).
builder.AddFlowzaForwardedHeaders();
builder.AddIdentityInfrastructure();
builder.AddIdentityLoginKeyProtection();
builder.AddIdentityTokenServer();

// Identity checks its own access tokens against its local token server, including whether they were revoked (K7).
builder.AddFlowzaApiSecurity(validation =>
{
    validation.UseLocalServer();
    validation.EnableTokenEntryValidation();
});
builder.AddIdentityLoginSession();
builder.AddInstallationBrand();
builder.Services.AddOpenApi();

var app = builder.Build();

// `--database-scripts <folder>`: write the production database scripts and stop (PLT-03-AC2, PL4; T033).
if (await app.TryWriteDatabaseScriptsAsync<IdentityServiceDbContext>("identity"))
{
    return;
}

app.UseFlowzaForwardedHeaders();
app.UseFlowzaErrorHandling();
app.UseIdentityLoginPages();
app.UseAuthentication();
app.UseAuthorization();
app.MapFlowzaDefaultEndpoints();
app.MapConnectEndpoints();
app.MapRazorPages();
app.MapUserEndpoints();
app.MapUnitTypeEndpoints();
app.MapOrgUnitEndpoints();
app.MapMeEndpoints();
app.MapInstallationEndpoints();

if (app.Environment.IsDevelopment())
{
    // The API description for the web app's generated client (orval, T025); development only.
    app.MapOpenApi().AllowAnonymous();
}

// Small, login-protected endpoint that tells which service answered (used by Gateway and tests).
app.MapGet("/api/v1/info", () => new { service = "identity" });


await app.RunAsync();
