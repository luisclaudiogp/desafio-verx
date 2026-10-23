using Consolidado.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;

namespace Consolidado.UnitTests.Infrastructure;

public class FailOpenCacheTests
{
    private static MemoryDistributedCache EmMemoria() => new(Options.Create(new MemoryDistributedCacheOptions()));

    private static FailOpenCache Criar(IDistributedCache cache, ResiliencePipeline? pipeline = null) =>
        new(cache, pipeline ?? ResiliencePipeline.Empty, NullLogger<FailOpenCache>.Instance);

    [Fact]
    public async Task SetEGet_CacheSaudavel_RoundTrip()
    {
        var sut = Criar(EmMemoria());

        await sut.SetStringAsync("k", "valor", TimeSpan.FromMinutes(1), default);

        Assert.Equal("valor", await sut.GetStringAsync("k", default));
    }

    [Fact]
    public async Task Remove_CacheSaudavel_ApagaEntrada()
    {
        var sut = Criar(EmMemoria());
        await sut.SetStringAsync("k", "valor", TimeSpan.FromMinutes(1), default);

        await sut.RemoveAsync("k", default);

        Assert.Null(await sut.GetStringAsync("k", default));
    }

    [Fact]
    public async Task Get_ChaveInexistente_RetornaNulo() =>
        Assert.Null(await Criar(EmMemoria()).GetStringAsync("nada", default));

    [Fact]
    public async Task Get_RedisFora_RetornaNuloSemLancar() =>
        Assert.Null(await Criar(new CacheQuebrado()).GetStringAsync("k", default));

    [Fact]
    public async Task Set_RedisFora_NaoLanca() =>
        await Criar(new CacheQuebrado()).SetStringAsync("k", "v", TimeSpan.FromMinutes(1), default);

    [Fact]
    public async Task Remove_RedisFora_NaoLanca() =>
        await Criar(new CacheQuebrado()).RemoveAsync("k", default);

    [Fact]
    public async Task CircuitBreakerAberto_ParaDeTocarNoCache()
    {
        var cache = new CacheQuebrado();
        var pipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 2,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromMinutes(5)
            })
            .Build();
        var sut = Criar(cache, pipeline);

        for (var i = 0; i < 10; i++)
            await sut.GetStringAsync("k", default);

        Assert.True(cache.Chamadas < 10);
    }

    [Fact]
    public async Task CancelamentoDoChamador_Propaga()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => Criar(new CacheQuebrado()).GetStringAsync("k", cts.Token));
    }

    [Fact]
    public void PipelinePadrao_Cria() => Assert.NotNull(FailOpenCache.CriarPipelinePadrao());

    private sealed class CacheQuebrado : IDistributedCache
    {
        public int Chamadas { get; private set; }

        private IOException Falha()
        {
            Chamadas++;
            return new IOException("redis fora do ar");
        }

        public byte[]? Get(string key) => throw Falha();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Falha();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Falha();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Falha();
        public void Refresh(string key) => throw Falha();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Falha();
        public void Remove(string key) => throw Falha();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Falha();
    }
}
