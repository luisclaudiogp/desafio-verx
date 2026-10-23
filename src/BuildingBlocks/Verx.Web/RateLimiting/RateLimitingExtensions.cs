using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Verx.Web.Security;

namespace Verx.Web.RateLimiting;

public static class RateLimitingExtensions
{
    public const string PolicyPadrao = "verx-padrao";

    private const string ChaveGlobal = "global";

    public static IServiceCollection AddVerxRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var opcoes = configuration.GetSection(RateLimitingOptions.Section).Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.AddSingleton(opcoes);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<LocalTokenBucketStore>();
        services.TryAddSingleton<ITokenBucketStore>(provedor => provedor.GetRequiredService<LocalTokenBucketStore>());

        return services
            .AddMiddlewareDeRateLimit()
            .AddPoliticasPadrao(opcoes);
    }

    public static IServiceCollection AddVerxRateLimitPolicy(
        this IServiceCollection services,
        string nome,
        Func<HttpContext, string> chaveDaParticao,
        int capacidade,
        double reposicaoPorSegundo) =>
        services.AddOptions<RateLimiterOptions>().Configure<ITokenBucketStore, TimeProvider>((options, store, time) =>
            options.AddPolicy(nome, context =>
                RateLimitPartition.Get(
                    chaveDaParticao(context),
                    chave => new TokenBucketStoreRateLimiter(store, nome + ":" + chave, capacidade, reposicaoPorSegundo, time))))
            .Services;

    private static IServiceCollection AddMiddlewareDeRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (rejected, _) =>
            {
                rejected.HttpContext.Response.Headers.RetryAfter = "1";
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                    rejected.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retry.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };
        });

    private static IServiceCollection AddPoliticasPadrao(this IServiceCollection services, RateLimitingOptions opcoes) =>
        services.AddOptions<RateLimiterOptions>().Configure<ITokenBucketStore, TimeProvider>((options, store, time) =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                RateLimitPartition.Get(
                    ChaveGlobal,
                    chave => new TokenBucketStoreRateLimiter(
                        store, chave, opcoes.PermitLimitGlobal, opcoes.PermitLimitGlobal, time)));

            options.AddPolicy(PolicyPadrao, context =>
                RateLimitPartition.Get(
                    ChaveParticao(context),
                    chave => new TokenBucketStoreRateLimiter(
                        store, PolicyPadrao + ":" + chave, opcoes.PermitLimitPorUsuario, opcoes.ReposicaoPorSegundo, time)));
        }).Services;

    private static string ChaveParticao(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
            ? "u:" + context.User.Usuario()
            : "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido");
}
