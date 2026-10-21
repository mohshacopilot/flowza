using Flowza.Design.Infrastructure.Persistence;
using Flowza.Design.Infrastructure;
using Flowza.Persistence;
using Flowza.ServiceDefaults;
using Flowza.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowzaServiceDefaults();
builder.AddFlowzaApiSecurity();
builder.AddDesignInfrastructure();

var app = builder.Build();

app.UseFlowzaErrorHandling();
app.UseAuthentication();
app.UseAuthorization();
app.MapFlowzaDefaultEndpoints();

// Small, login-protected endpoint that tells which service answered (used by Gateway and tests).
app.MapGet("/api/v1/info", () => new { service = "design" });

if (app.Environment.IsDevelopment())
{
    // Development only: production applies reviewed SQL scripts (PLT-03, PL4).
    await app.Services.MigrateDatabaseAsync<DesignDbContext>();
}

await app.RunAsync();