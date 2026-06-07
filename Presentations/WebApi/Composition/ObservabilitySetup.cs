using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace WebApi.Composition;

public static class ObservabilitySetup
{
    /// <summary>
    /// Wires OpenTelemetry tracing + metrics (ASP.NET Core, HttpClient, the GymBro.Cache and GymBro.Outbox
    /// meters). OTLP export is <b>opt-in</b>: enabled only when an endpoint is configured
    /// (<c>OpenTelemetry:OtlpEndpoint</c> or the standard <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>), so dev/test
    /// runs collect in-process without emitting noisy exporter connection failures.
    /// </summary>
    public static IServiceCollection AddGymBroObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"]
            ?? configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var exportEnabled = !string.IsNullOrWhiteSpace(otlpEndpoint)
            && Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out _);
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "gymbro-api";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
                if (exportEnabled)
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint!));
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter("GymBro.Cache")
                    .AddMeter("GymBro.Outbox");
                if (exportEnabled)
                    metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint!));
            });

        return services;
    }
}
