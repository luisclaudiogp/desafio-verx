using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Exceptions;
using Verx.SharedKernel;

namespace Lancamentos.UnitTests.Domain;

public class PosicaoContaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Conta = Guid.NewGuid();

    private static Lancamento Lanc(TipoMovimento tipo, decimal valor, string chave = "k") =>
        Lancamento.Registrar(LancamentoTests.Dados(tipo, valor, chave, Conta), Agora);

    [Fact]
    public void Abrir_ComecaZerada()
    {
        var p = PosicaoConta.Abrir(Conta);

        Assert.Equal(Conta, p.ContaId);
        Assert.Equal(0m, p.Saldo);
    }

    [Fact]
    public void Abrir_ContaVazia_Lanca() =>
        Assert.Throws<DomainException>(() => PosicaoConta.Abrir(Guid.Empty));

    [Fact]
    public void Aplicar_CreditoSomaEDebitoSubtrai()
    {
        var p = PosicaoConta.Abrir(Conta);

        p.Aplicar(Lanc(TipoMovimento.Credito, 100m));
        p.Aplicar(Lanc(TipoMovimento.Debito, 30m, "k2"));

        Assert.Equal(70m, p.Saldo);
    }

    [Fact]
    public void Aplicar_DebitoIgualAoSaldo_Permite()
    {
        var p = PosicaoConta.Abrir(Conta);
        p.Aplicar(Lanc(TipoMovimento.Credito, 50m));

        p.Aplicar(Lanc(TipoMovimento.Debito, 50m, "k2"));

        Assert.Equal(0m, p.Saldo);
    }

    [Fact]
    public void Aplicar_DebitoMaiorQueOSaldo_LancaSemAlterarSaldo()
    {
        var p = PosicaoConta.Abrir(Conta);
        p.Aplicar(Lanc(TipoMovimento.Credito, 50m));

        Assert.Throws<SaldoInsuficienteException>(() => p.Aplicar(Lanc(TipoMovimento.Debito, 50.01m, "k2")));

        Assert.Equal(50m, p.Saldo);
    }

    [Fact]
    public void Aplicar_EstornoDeCreditoJaGasto_NaoEBloqueadoEDeixaSaldoNegativo()
    {
        var p = PosicaoConta.Abrir(Conta);
        var credito = Lanc(TipoMovimento.Credito, 100m);
        p.Aplicar(credito);
        p.Aplicar(Lanc(TipoMovimento.Debito, 90m, "k2"));

        p.Aplicar(credito.Estornar("est", "u", Agora));

        Assert.Equal(-90m, p.Saldo);
    }

    [Fact]
    public void Aplicar_LancamentoDeOutraConta_Lanca()
    {
        var p = PosicaoConta.Abrir(Guid.NewGuid());

        Assert.Throws<DomainException>(() => p.Aplicar(Lanc(TipoMovimento.Credito, 1m)));
    }
}
