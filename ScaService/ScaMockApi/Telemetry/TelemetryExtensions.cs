using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace ScaMockApi.Telemetry;

public static class TelemetryExtensions
{
    private const string HealthCheckPath = "/api/healthcheck";

    /// <summary>
    /// Configures OpenTelemetry metrics, traces and logs. Data is exported via OTLP when
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set, which Aspire injects so it shows in the dashboard.
    /// </summary>
    public static WebApplicationBuilder AddScaTelemetry(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ScaMetrics>();

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(ScaMetrics.MeterName))
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddAspNetCoreInstrumentation(options =>
                    // Keep health-check polling out of traces.
                    options.Filter = context => !context.Request.Path.StartsWithSegments(HealthCheckPath))
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }
}
