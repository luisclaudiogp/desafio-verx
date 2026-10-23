using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Verx.Web.RateLimiting;

namespace Verx.Web.Redis;

public static class RedisRateLimitingExtensions
{
    public static IServiceCollection AddVerxRedisRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var opcoes = configuration.GetSection(RedisRateLimitingOptions.Section).Get<RedisRateLimitingOptions>()
                     ?? new RedisRateLimitingOptions();
        if (!opcoes.Habilitado)
            return services;

        services.AddSingleton<ITokenBucketStore>(provedor => CriarStoreDistribuido(provedor, opcoes));
        return services;
    }

    private static ITokenBucketStore CriarStoreDistribuido(IServiceProvider provedor, RedisRateLimitingOptions opcoes)
    {
        var local = provedor.GetRequiredService<LocalTokenBucketStore>();
        var logger = provedor.GetRequiredService<ILoggerFactory>().CreateLogger<ResilientTokenBucketStore>();
        var tempo = provedor.GetRequiredService<TimeProvider>();

        try
        {
            var redis = ConnectionMultiplexer.Connect(opcoes.ConnectionString!);
            return new ResilientTokenBucketStore(
                new RedisTokenBucketStore(redis),
                local,
                ResilientTokenBucketStore.CriarPipelinePadrao(TimeSpan.FromMilliseconds(opcoes.TimeoutMs), tempo),
                logger);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Nao foi possivel conectar ao Redis do rate limit; usando apenas o limite local.");
            return local;
        }
    }
}
