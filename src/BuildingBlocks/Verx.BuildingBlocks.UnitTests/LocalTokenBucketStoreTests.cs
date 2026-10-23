using Verx.Web.RateLimiting;

namespace Verx.BuildingBlocks.UnitTests;

public class LocalTokenBucketStoreTests
{
    private readonly RelogioManual _relogio = new();
    private readonly LocalTokenBucketStore _sut;

    public LocalTokenBucketStoreTests()
    {
        _sut = new LocalTokenBucketStore(_relogio);
    }

    private async Task<bool> Consumir(string chave = "k", int capacidade = 3, double reposicao = 1) =>
        (await _sut.TentarConsumirAsync(chave, capacidade, reposicao, CancellationToken.None)).Permitido;

    [Fact]
    public async Task PermiteAteACapacidadeEDepoisNega()
    {
        Assert.True(await Consumir());
        Assert.True(await Consumir());
        Assert.True(await Consumir());
        Assert.False(await Consumir());
    }

    [Fact]
    public async Task NegativaInformaQuantoEsperar()
    {
        for (var i = 0; i < 3; i++)
            await Consumir(reposicao: 2);

        var negado = await _sut.TentarConsumirAsync("k", 3, 2, CancellationToken.None);

        Assert.False(negado.Permitido);
        Assert.Equal(TimeSpan.FromMilliseconds(500), negado.RetryAfter);
    }

    [Fact]
    public async Task ReponhaOsTokensComOPassarDoTempo()
    {
        for (var i = 0; i < 3; i++)
            await Consumir();
        Assert.False(await Consumir());

        _relogio.Avancar(TimeSpan.FromSeconds(1));

        Assert.True(await Consumir());
        Assert.False(await Consumir());
    }

    [Fact]
    public async Task ReposicaoNuncaUltrapassaACapacidade()
    {
        await Consumir();
        _relogio.Avancar(TimeSpan.FromHours(1));

        for (var i = 0; i < 3; i++)
            Assert.True(await Consumir());
        Assert.False(await Consumir());
    }

    [Fact]
    public async Task ChavesSaoIndependentes()
    {
        for (var i = 0; i < 3; i++)
            await Consumir("a");

        Assert.False(await Consumir("a"));
        Assert.True(await Consumir("b"));
    }

    [Fact]
    public async Task ConcorrenciaNaoPermiteMaisQueACapacidade()
    {
        var resultados = await Task.WhenAll(Enumerable.Range(0, 200)
            .Select(_ => Task.Run(() => Consumir("quente", capacidade: 50, reposicao: 0.001))));

        Assert.Equal(50, resultados.Count(r => r));
    }

    [Fact]
    public async Task BaldesOciososSaoRemovidosSemPerderRegra()
    {
        for (var i = 0; i < 20; i++)
            await Consumir($"u{i}", capacidade: 2, reposicao: 1);
        Assert.Equal(20, _sut.QuantidadeDeBaldes);

        _relogio.Avancar(TimeSpan.FromMinutes(5));
        await Consumir("novo", capacidade: 2, reposicao: 1);

        Assert.Equal(1, _sut.QuantidadeDeBaldes);
        Assert.True(await Consumir("u0", capacidade: 2, reposicao: 1));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public async Task ParametrosInvalidos_Lancam(int capacidade, double reposicao) =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await _sut.TentarConsumirAsync("k", capacidade, reposicao, CancellationToken.None));

    [Fact]
    public async Task ChaveVazia_Lanca() =>
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _sut.TentarConsumirAsync("", 1, 1, CancellationToken.None));
}
