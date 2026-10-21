var builder = DistributedApplication.CreateBuilder(args);

// Wired fully in task T006 (connection strings, RabbitMQ, smtp4dev, web app).
var identity = builder.AddProject<Projects.Flowza_Identity_Api>("identity");
var design = builder.AddProject<Projects.Flowza_Design_Api>("design");
var runtime = builder.AddProject<Projects.Flowza_Runtime_Api>("runtime");
var worker = builder.AddProject<Projects.Flowza_Worker>("worker");

builder.AddProject<Projects.Flowza_Gateway>("gateway")
    .WithReference(identity)
    .WithReference(design)
    .WithReference(runtime)
    .WithReference(worker);

await builder.Build().RunAsync();