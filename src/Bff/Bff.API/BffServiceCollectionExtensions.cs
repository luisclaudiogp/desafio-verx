using Bff.API.Auth;
using Bff.API.Gateway;
using Bff.API.Sessao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Verx.Web.Errors;
using Verx.Web.Observability;
using Verx.Web.RateLimiting;
using Verx.Web.Redis;
using Verx.Web.OpenApi;
using Verx.Web.Security;

namespace Bff.API;

public static class BffServiceCollectionExtensions
{
    public const string ServicoNome = "Bff.API";

    public static IServiceCollection AddBff(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services
            .AddVerxSecurity(configuration)
            .AddVerxRateLimiting(configuration)
            .AddVerxRedisRateLimiting(configuration)
            .AddVerxObservability(configuration, ServicoNome)
            .AddVerxOpenApi();

        services.AddSingleton<IAuthorizationHandler, ContaDaRotaAuthorizationHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy(GatewayPolicies.AcessoContaDaRota, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new ContaDaRotaRequirement()));

        var loginPorMinuto = configuration.GetValue("RateLimiting:LoginPorMinuto", 20);
        services.AddVerxRateLimitPolicy(
            GatewayPolicies.Login,
            context => context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            loginPorMinuto,
            loginPorMinuto / 60d);

        services.AddOptions<KeycloakOptions>()
            .Bind(configuration.GetSection(KeycloakOptions.Secao))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<ITokenProvider, KeycloakTokenProvider>()
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 1;
                options.Retry.Delay = TimeSpan.FromMilliseconds(300);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            });

        services.AddExceptionHandler<IdentidadeExceptionHandler>();
        services.AddExceptionHandler<VerxExceptionHandler>();
        services.AddProblemDetails();
        services.AddSingleton(TimeProvider.System);
        services.AddControllers();

        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms(transforms => transforms.RequestTransforms.Add(new TokenDaSessaoRequestTransform()));

        services.AddSessaoBff(configuration, environment);

        services.AddHealthChecks();
        AddCors(services, configuration);

        return services;
    }

    private static void AddCors(IServiceCollection services, IConfiguration configuration)
    {
        var origens = configuration.GetSection("Cors:Origens").Get<string[]>() ?? [];
        if (origens.Length == 0)
            return;

        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(origens)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Location", "Retry-After")));
    }
}
