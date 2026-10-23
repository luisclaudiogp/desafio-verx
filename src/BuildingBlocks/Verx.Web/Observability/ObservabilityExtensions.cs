using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Verx.Web.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddVerxObservability(
        this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        var otlp = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource("MassTransit")
                    .AddAspNetCoreInstrumentation(o =>
                        o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
                                          && !ctx.Request.Path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase))
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlp))
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
            })
            .WithMetrics(metrics => metrics
                .AddMeter("MassTransit")
                .AddMeter("Verx.*")
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter());

        return services;
    }
}
