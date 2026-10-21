using System.Globalization;
using Flowza.ServiceDefaults.Http;
using Flowza.ServiceDefaults.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace Flowza.ServiceDefaults;

/// <summary>
/// What every Flowza service and the Gateway share: structured logging with the privacy guard, OpenTelemetry
/// traces and metrics, health checks, ProblemDetails with a reference code, and service discovery
/// (plan 001 §8; tech discussion T7).
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>Health checks with this tag run in <c>/health/live</c>; all checks run in <c>/health/ready</c>.</summary>
    public const string LiveTag = "live";

    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    /// <summary>Activity sources and meters of Flowza code start with this name.</summary>
    public const string TelemetryPrefix = "Flowza";

    /// <summary>Activity source and meter name used by Wolverine (messaging).</summary>
    public const string WolverineTelemetryName = "Wolverine";

    public static TBuilder AddFlowzaServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddFlowzaLogging();
        builder.AddFlowzaTelemetry();

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag]);

        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["reference"] = ReferenceCode.For(context.HttpContext));

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http => http.AddServiceDiscovery());

        return builder;
    }

    /// <summary>
    /// Serilog with the masking enricher. Development: logs go to the console and to the Aspire dashboard
    /// (OpenTelemetry). Production: daily files kept 30 days (OB5), plus OpenTelemetry when an endpoint is configured.
    /// </summary>
    public static TBuilder AddFlowzaLogging<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        var serviceName = builder.Environment.ApplicationName;
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var logFolder = builder.Configuration["Flowza:Logging:Folder"];
        var writeFiles = !builder.Environment.IsDevelopment() || !string.IsNullOrWhiteSpace(logFolder);

        // Every line carries the service and the trace ID, so the reference code on an error screen (its first
        // 8 characters, OB4) can be found in the daily files, the only logs on-premises.
        const string template =
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Service} {TraceId} {SourceContext}: {Message:lj}{NewLine}{Exception}";

        builder.Services.AddSerilog(logger =>
        {
            logger
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", serviceName)
                .Enrich.With<SensitiveDataMaskingEnricher>()
                .WriteTo.Console(outputTemplate: template, formatProvider: CultureInfo.InvariantCulture);

            if (writeFiles)
            {
                // Relative to the service's own folder, not the working directory (under IIS or a Windows
                // service that would be System32).
                var folder = Path.Combine(builder.Environment.ContentRootPath, string.IsNullOrWhiteSpace(logFolder) ? "logs" : logFolder);
                logger.WriteTo.File(
                    Path.Combine(folder, $"{serviceName}-.log"),
                    outputTemplate: template,
                    formatProvider: CultureInfo.InvariantCulture,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: builder.Configuration.GetValue("Flowza:Logging:RetainedDays", 30));
            }

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                logger.WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.ResourceAttributes = new Dictionary<string, object> { ["service.name"] = serviceName };
                });
            }
        });

        return builder;
    }

    public static TBuilder AddFlowzaTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        var telemetry = builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter($"{TelemetryPrefix}.*")
                .AddMeter(WolverineTelemetryName))
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddSource($"{TelemetryPrefix}.*")
                // Wolverine starts an activity for each message sent and handled; without it the trace
                // reference stops at the service boundary (PLT-05-AC2).
                .AddSource(WolverineTelemetryName)
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }

    /// <summary>Maps <c>/health/live</c> and <c>/health/ready</c>; both are open without login (IAM-05).</summary>
    public static WebApplication MapFlowzaDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(LiveTag) })
            .AllowAnonymous();
        app.MapHealthChecks(ReadyPath, new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync })
            .AllowAnonymous();

        return app;
    }

    /// <summary>Exception handler and status-code pages that answer with ProblemDetails (and a reference code).</summary>
    public static WebApplication UseFlowzaErrorHandling(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }
}
