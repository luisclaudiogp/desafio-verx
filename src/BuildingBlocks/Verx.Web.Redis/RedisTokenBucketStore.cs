using System.Globalization;
using StackExchange.Redis;
using Verx.Web.RateLimiting;

namespace Verx.Web.Redis;

public sealed class RedisTokenBucketStore(IConnectionMultiplexer redis) : ITokenBucketStore
{
    private const string Prefixo = "ratelimit:";

    private static readonly LuaScript Script = LuaScript.Prepare("""
        local t = redis.call('TIME')
        local agora = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)
        local dados = redis.call('HMGET', @chave, 'tokens', 'ts')
        local tokens = tonumber(dados[1])
        local ts = tonumber(dados[2])
        if tokens == nil then
            tokens = tonumber(@capacidade)
            ts = agora
        end
        local decorrido = math.max(0, agora - ts)
        tokens = math.min(tonumber(@capacidade), tokens + decorrido * tonumber(@reposicao) / 1000)
        local permitido = 0
        local espera = 0
        if tokens >= 1 then
            tokens = tokens - 1
            permitido = 1
        else
            espera = math.ceil((1 - tokens) * 1000 / tonumber(@reposicao))
        end
        redis.call('HSET', @chave, 'tokens', tostring(tokens), 'ts', tostring(agora))
        redis.call('PEXPIRE', @chave, math.ceil(tonumber(@capacidade) / tonumber(@reposicao) * 1000) * 2 + 1000)
        return {permitido, espera}
        """);

    public async ValueTask<ResultadoTokenBucket> TentarConsumirAsync(
        string chave, int capacidade, double reposicaoPorSegundo, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(chave);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacidade, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(reposicaoPorSegundo, 0d);

        var resposta = (RedisResult[]?)await Script.EvaluateAsync(
            redis.GetDatabase(),
            new
            {
                chave = (RedisKey)(Prefixo + chave),
                capacidade,
                reposicao = reposicaoPorSegundo.ToString("R", CultureInfo.InvariantCulture)
            }) ?? throw new InvalidOperationException("Resposta vazia do Redis.");

        var permitido = (long)resposta[0] == 1;
        var esperaMs = (long)resposta[1];
        return new ResultadoTokenBucket(permitido, permitido ? null : TimeSpan.FromMilliseconds(esperaMs));
    }
}
