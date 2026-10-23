using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Testcontainers.Redis;
using Verx.IntegrationTests.Infra;
using Verx.Web.RateLimiting;
using Verx.Web.Redis;

namespace Verx.IntegrationTests;

[Collection(RedisTestes.Nome)]
public class RateLimitDistribuidoTests(RedisFixture redis)
{
    private static string ChaveUnica() => "t-" + Guid.NewGuid().ToString("N");

    private static async Task<int> Permitidos(ITokenBucketStore store, string chave, int tentativas, int capacidade, double reposicao)
    {
        var resultados = await Task.WhenAll(Enumerable.Range(0, tentativas)
            .Select(_ => Task.Run(async () => (await store.TentarConsumirAsync(chave, capacidade, reposicao, CancellationToken.None)).Permitido)));
        return resultados.Count(r => r);
    }

    [DockerFact]
    public async Task DuasReplicas_CompartilhamAMesmaCota()
    {
        await using var conexaoA = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        await using var conexaoB = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var replicaA = new RedisTokenBucketStore(conexaoA);
        var replicaB = new RedisTokenBucketStore(conexaoB);
        var chave = ChaveUnica();

        var permitidos = await Task.WhenAll(
            Permitidos(replicaA, chave, 60, capacidade: 40, reposicao: 0.001),
            Permitidos(replicaB, chave, 60, capacidade: 40, reposicao: 0.001));

        Assert.Equal(40, permitidos.Sum());
    }

    [Fact]
    public async Task LimiteLocalPorReplica_PermitiriaOTotalDobrado()
    {
        var local = Enumerable.Range(0, 2).Select(_ => new LocalTokenBucketStore(TimeProvider.System)).ToArray();
        var chave = ChaveUnica();

        var permitidos = await Task.WhenAll(local.Select(l => Permitidos(l, chave, 60, 40, 0.001)));

        Assert.Equal(80, permitidos.Sum());
    }

    [DockerFact]
    public async Task ReposicaoLiberaNovasRequisicoes_ENegativaInformaOTempoDeEspera()
    {
        await using var conexao = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var store = new RedisTokenBucketStore(conexao);
        var chave = ChaveUnica();

        Assert.True((await store.TentarConsumirAsync(chave, 2, 10, CancellationToken.None)).Permitido);
        Assert.True((await store.TentarConsumirAsync(chave, 2, 10, CancellationToken.None)).Permitido);
        var negado = await store.TentarConsumirAsync(chave, 2, 10, CancellationToken.None);

        Assert.False(negado.Permitido);
        Assert.InRange(negado.RetryAfter!.Value.TotalMilliseconds, 1, 100);

        await Task.Delay(300);

        Assert.True((await store.TentarConsumirAsync(chave, 2, 10, CancellationToken.None)).Permitido);
    }

    [DockerFact]
    public async Task ChavesDiferentesTemCotasIndependentes()
    {
        await using var conexao = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var store = new RedisTokenBucketStore(conexao);

        Assert.Equal(5, await Permitidos(store, ChaveUnica(), 20, 5, 0.001));
        Assert.Equal(5, await Permitidos(store, ChaveUnica(), 20, 5, 0.001));
    }

    [DockerFact]
    public async Task ChaveDoRedisExpiraQuandoOciosa()
    {
        await using var conexao = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var store = new RedisTokenBucketStore(conexao);
        var chave = ChaveUnica();

        await store.TentarConsumirAsync(chave, 1, 1000, CancellationToken.None);

        var ttl = await conexao.GetDatabase().KeyTimeToLiveAsync("ratelimit:" + chave);
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value.TotalSeconds, 0.1, 5);
    }

    [DockerFact]
    public async Task RedisFora_RateLimitContinuaProtegendoLocalmenteERetomaAoVoltar()
    {
        await using var container = new RedisBuilder("redis:7-alpine").Build();
        await container.StartAsync();
        var conexao = await ConnectionMultiplexer.ConnectAsync(
            container.GetConnectionString() + ",abortConnect=false,connectTimeout=500,syncTimeout=500,asyncTimeout=500");
        var local = new LocalTokenBucketStore(TimeProvider.System);
        var store = new ResilientTokenBucketStore(
            new RedisTokenBucketStore(conexao),
            local,
            ResilientTokenBucketStore.CriarPipelinePadrao(TimeSpan.FromMilliseconds(300), TimeProvider.System),
            NullLogger<ResilientTokenBucketStore>.Instance);
        var chave = ChaveUnica();

        Assert.Equal(3, await Permitidos(store, chave, 10, 3, 0.001));
        await container.StopAsync();
        var chaveLocal = ChaveUnica();

        var comRedisFora = await Permitidos(store, chaveLocal, 10, 4, 0.001);

        Assert.Equal(4, comRedisFora);
        await conexao.DisposeAsync();
    }
}
