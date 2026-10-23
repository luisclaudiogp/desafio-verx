using Lancamentos.Domain.Entities;
using Verx.SharedKernel;

namespace Lancamentos.UnitTests.Domain;

public class LancamentoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Conta = Guid.NewGuid();

    internal static NovoLancamento Dados(
        TipoMovimento tipo = TipoMovimento.Credito,
        decimal valor = 100m,
        string chave = "k1",
        Guid? conta = null,
        DateTimeOffset? ocorridoEm = null) =>
        new(conta ?? Conta, tipo, valor, ocorridoEm, chave, "user-1");

    private static Lancamento Novo(TipoMovimento tipo = TipoMovimento.Credito, decimal valor = 100m, string chave = "k1") =>
        Lancamento.Registrar(Dados(tipo, valor, chave), Agora);

    [Fact]
    public void Registrar_DadosValidos_PreencheCampos()
    {
        var l = Novo(TipoMovimento.Debito, 10.50m);

        Assert.NotEqual(Guid.Empty, l.Id);
        Assert.Equal(Conta, l.ContaId);
        Assert.Equal(TipoMovimento.Debito, l.Tipo);
        Assert.Equal(10.50m, l.Valor.Valor);
        Assert.Equal(Agora, l.OcorridoEm);
        Assert.Equal(Agora, l.RegistradoEm);
        Assert.Equal("user-1", l.RegistradoPor);
        Assert.Null(l.EstornoDeId);
    }

    [Fact]
    public void Registrar_ComOcorridoEmInformado_UsaDataInformada()
    {
        var ocorrido = Agora.AddDays(-3);
        var l = Lancamento.Registrar(Dados(ocorridoEm: ocorrido), Agora);
        Assert.Equal(ocorrido, l.OcorridoEm);
    }

    [Fact]
    public void Registrar_DadosNulos_Lanca() =>
        Assert.Throws<ArgumentNullException>(() => Lancamento.Registrar(null!, Agora));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.123)]
    public void Registrar_ValorInvalido_Lanca(double valor) =>
        Assert.Throws<DomainException>(() => Novo(valor: (decimal)valor));

    [Fact]
    public void Registrar_ContaVazia_Lanca() =>
        Assert.Throws<DomainException>(() => Lancamento.Registrar(Dados(conta: Guid.Empty), Agora));

    [Fact]
    public void Registrar_TipoInvalido_Lanca() =>
        Assert.Throws<DomainException>(() => Lancamento.Registrar(Dados(tipo: (TipoMovimento)99), Agora));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Registrar_ChaveVazia_Lanca(string chave) =>
        Assert.Throws<DomainException>(() => Novo(chave: chave));

    [Fact]
    public void Registrar_ChaveMuitoLonga_Lanca() =>
        Assert.Throws<DomainException>(() => Novo(chave: new string('x', Lancamento.TamanhoMaximoChaveIdempotencia + 1)));

    [Fact]
    public void Registrar_DataNoFuturo_Lanca() =>
        Assert.Throws<DomainException>(() => Lancamento.Registrar(Dados(ocorridoEm: Agora.AddHours(1)), Agora));

    [Theory]
    [InlineData(TipoMovimento.Credito, TipoMovimento.Debito)]
    [InlineData(TipoMovimento.Debito, TipoMovimento.Credito)]
    public void Estornar_GeraLancamentoDeTipoOpostoEMesmoValor(TipoMovimento original, TipoMovimento esperado)
    {
        var l = Novo(original, 75.25m);

        var estorno = l.Estornar("k-estorno", "user-2", Agora.AddMinutes(1));

        Assert.Equal(esperado, estorno.Tipo);
        Assert.Equal(75.25m, estorno.Valor.Valor);
        Assert.Equal(l.Id, estorno.EstornoDeId);
        Assert.Equal(l.ContaId, estorno.ContaId);
        Assert.NotEqual(l.Id, estorno.Id);
        Assert.Equal("user-2", estorno.RegistradoPor);
    }

    [Fact]
    public void Estornar_UmEstorno_Lanca()
    {
        var estorno = Novo().Estornar("k2", "u", Agora);
        Assert.Throws<DomainException>(() => estorno.Estornar("k3", "u", Agora));
    }

    [Fact]
    public void Estornar_ChaveInvalida_Lanca() =>
        Assert.Throws<DomainException>(() => Novo().Estornar(" ", "u", Agora));
}
