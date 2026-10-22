using Flowza.Gateway.Protection;
using Flowza.Gateway;
using Flowza.Gateway.Proxy;
using Flowza.Gateway.Session;
using Flowza.Gateway.WebApp;
using Flowza.ServiceDefaults;
using Flowza.ServiceDefaults.Http;

// The single entry point: routes to the services and Identity's login pages (YARP, GW1), the BFF session (SE1;
// task T020), the protection around both (task T021) and, outside Development, the built web app (GW2, T030).
var builder = WebApplication.CreateBuilder(args);

builder.AddFlowzaServiceDefaults();
builder.AddFlowzaForwardedHeaders();
builder.AddGatewaySession();
builder.AddGatewayProtection();
builder.AddGatewayProxy();

var app = builder.Build();

// A real installation names its host(s), so requests for other host names are refused (T030 item b).
if (!app.Environment.IsDevelopment() && app.Configuration["AllowedHosts"] is null or "*")
{
    GatewayStartupLog.AllowedHostsOpen(app.Logger);
}

app.UseFlowzaForwardedHeaders();
app.UseFlowzaErrorHandling();
app.UseGatewayEdgeProtection();
app.UseGatewayWebAppFiles();
app.UseAuthentication();
app.UseGatewayRequestProtection();
app.UseAuthorization();
app.MapFlowzaDefaultEndpoints();
app.MapBffEndpoints();
app.MapReverseProxy();
app.MapGatewayWebApp();

await app.RunAsync();
