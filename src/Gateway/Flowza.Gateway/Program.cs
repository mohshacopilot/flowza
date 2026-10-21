using Flowza.ServiceDefaults;

// The Gateway's routes, BFF login and protection arrive in tasks T020–T021; for now it starts with the
// shared defaults (logging, telemetry, health).
var builder = WebApplication.CreateBuilder(args);

builder.AddFlowzaServiceDefaults();

var app = builder.Build();

app.UseFlowzaErrorHandling();
app.MapFlowzaDefaultEndpoints();

await app.RunAsync();
