using System.Text.Json.Serialization;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Scalar.AspNetCore;
using Verx.Web.Errors;
using Verx.Web.Observability;
using Verx.Web.OpenApi;
using Verx.Web.RateLimiting;
using Verx.Web.Security;

namespace Verx.Web.Hosting;

public static class ApiHostingExtensions
{
    public const string HealthPostgres = "postgres";

    public static IServiceCollection AddVerxApi(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        services
            .AddVerxSecurity(configuration)
            .AddVerxRateLimiting(configuration)
            .AddVerxObservability(configuration, serviceName)
            .AddVerxOpenApi();

        services.AddExceptionHandler<VerxExceptionHandler>();
        services.AddProblemDetails();
        services.AddSingleton(TimeProvider.System);
        services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        return services;
    }

    public static IHealthChecksBuilder AddVerxHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"]!,
            UserName = configuration["RabbitMQ:Username"]!,
            Password = configuration["RabbitMQ:Password"]!
        });

        return services.AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("DefaultConnection")!, name: HealthPostgres)
            .AddRabbitMQ(name: "rabbitmq", tags: ["broker"]);
    }

    public static WebApplication UseVerxApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Name == HealthPostgres,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });
        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        return app;
    }
}
