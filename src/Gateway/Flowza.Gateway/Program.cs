using Flowza.Gateway.Proxy;
using Flowza.Gateway.Session;
using Flowza.ServiceDefaults;

// The single entry point (task T020): routes to the services and Identity's login pages (YARP, GW1) and the BFF
// session (SE1). Protection (anti-forgery header, rate limits, security headers, body limits) follows in T021.
var builder = WebApplication.CreateBuilder(args);

builder.AddFlowzaServiceDefaults();
builder.AddGatewaySession();
builder.AddGatewayProxy();

var app = builder.Build();

app.UseFlowzaErrorHandling();
app.UseAuthentication();
app.UseAuthorization();
app.MapFlowzaDefaultEndpoints();
app.MapBffEndpoints();
app.MapReverseProxy();

await app.RunAsync();
