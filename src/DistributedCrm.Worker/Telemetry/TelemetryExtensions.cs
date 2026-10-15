using DistributedCrm.Infrastructure.Telemetry;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DistributedCrm.Worker.Telemetry;

public static class TelemetryExtensions
{
    // The service's identity in every backend (dashboard, App Insights' "cloud role name"), so the code owns it.
    public const string ServiceName = "distributed-crm-worker";

    // Exports traces, metrics and logs over OTLP. OTEL_EXPORTER_OTLP_ENDPOINT says where; unset means localhost:4317.
    public static IHostApplicationBuilder AddTelemetry(this IHostApplicationBuilder builder)
    {
        // The Service Bus SDK only emits Activities when this switch is on.
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

        // Opt-in, for debugging without the dashboard. Logs already reach the console through the default logger.
        var exportToConsole = builder.Configuration.GetValue<bool>("Telemetry:ConsoleExporter");

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    // The only HTTP traffic is platform probes. Tracing them adds nothing, and filtering the request
                    // also drops the health checks' SQL, Redis and Service Bus spans underneath it.
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                    .AddNpgsql()
                    .AddRedisInstrumentation()
                    .AddSource("Azure.Messaging.ServiceBus.*")
                    .AddSource(InfrastructureTelemetry.ActivitySourceName);

                if (exportToConsole)
                {
                    tracing.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddNpgsqlInstrumentation();

                if (exportToConsole)
                {
                    metrics.AddConsoleExporter();
                }
            })
            .WithLogging(configureBuilder: null, configureOptions: logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            })
            .UseOtlpExporter();

        return builder;
    }
}
