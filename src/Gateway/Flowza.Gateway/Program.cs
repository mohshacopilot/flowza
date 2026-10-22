using Flowza.Gateway.Protection;
using Flowza.Gateway.Proxy;
using Flowza.Gateway.Session;
using Flowza.ServiceDefaults;

// The single entry point: routes to the services and Identity's login pages (YARP, GW1), the BFF session (SE1;
// task T020) and the protection around both (task T021).
var builder = WebApplication.CreateBuilder(args);

builder.AddFlowzaServiceDefaults();
builder.AddGatewaySession();
builder.AddGatewayProtection();
builder.AddGatewayProxy();

var app = builder.Build();

app.UseFlowzaErrorHandling();
app.UseGatewayEdgeProtection();
app.UseAuthentication();
app.UseGatewayRequestProtection();
app.UseAuthorization();
app.MapFlowzaDefaultEndpoints();
app.MapBffEndpoints();
app.MapReverseProxy();

await app.RunAsync();
