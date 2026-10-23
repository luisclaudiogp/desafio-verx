using Lancamentos.Domain.Entities;
using Verx.SharedKernel;

namespace Lancamentos.UnitTests.Domain;

public class HashDaRequisicaoTests
{
    private static readonly DateTimeOffset Quando = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MesmoCorpo_MesmoHash() =>
        Assert.Equal(
            HashDaRequisicao.DoRegistro(TipoMovimento.Credito, 10m, Quando),
            HashDaRequisicao.DoRegistro(TipoMovimento.Credito, 10.00m, Quando));

    [Fact]
    public void HashTem64CaracteresHexadecimais() =>
        Assert.Matches("^[0-9A-F]{64}$", HashDaRequisicao.DoRegistro(TipoMovimento.Credito, 10m, null));

    [Fact]
    public void QualquerCampoDiferente_HashDiferente()
    {
        var baseHash = HashDaRequisicao.DoRegistro(TipoMovimento.Credito, 10m, Quando);

        Assert.NotEqual(baseHash, HashDaRequisicao.DoRegistro(TipoMovimento.Debito, 10m, Quando));
        Assert.NotEqual(baseHash, HashDaRequisicao.DoRegistro(TipoMovimento.Credito, 10.01m, Quando));
        Assert.NotEqual(baseHash, HashDaRequisicao.DoRegistro(TipoMovimento.Credito, 10m, Quando.AddSeconds(1)));
        Assert.NotEqual(baseHash, HashDaRequisicao.DoRegistro(TipoMovimento.Credito, 10m, null));
    }

    [Fact]
    public void MesmoInstanteEmOutroFuso_MesmoHash() =>
        Assert.Equal(
            HashDaRequisicao.DoRegistro(TipoMovimento.Credito, 10m, Quando),
            HashDaRequisicao.DoRegistro(TipoMovimento.Credito, 10m, Quando.ToOffset(TimeSpan.FromHours(-3))));

    [Fact]
    public void Estorno_DependeDoLancamentoEstornado()
    {
        var a = Guid.NewGuid();

        Assert.Equal(HashDaRequisicao.DoEstorno(a), HashDaRequisicao.DoEstorno(a));
        Assert.NotEqual(HashDaRequisicao.DoEstorno(a), HashDaRequisicao.DoEstorno(Guid.NewGuid()));
    }

    [Fact]
    public void RegistroNaoColideComEstorno() =>
        Assert.NotEqual(HashDaRequisicao.DoRegistro(TipoMovimento.Credito, 1m, null), HashDaRequisicao.DoEstorno(Guid.Empty));

    [Fact]
    public void Lancamento_CorrespondeAoProprioHash()
    {
        var l = Lancamento.Registrar(LancamentoTests.Dados(valor: 7m), Quando);

        Assert.True(l.CorrespondeAoHash(l.HashRequisicao));
        Assert.False(l.CorrespondeAoHash(HashDaRequisicao.DoRegistro(TipoMovimento.Credito, 8m, null)));
        Assert.Equal(l.HashRequisicao, HashDaRequisicao.DoRegistro(TipoMovimento.Credito, 7m, null));
    }
}
