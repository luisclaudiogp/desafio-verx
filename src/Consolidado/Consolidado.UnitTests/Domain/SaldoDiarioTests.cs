using Consolidado.Domain.Entities;
using Verx.SharedKernel;

namespace Consolidado.UnitTests.Domain;

public class SaldoDiarioTests
{
    private static readonly Guid Conta = Guid.NewGuid();
    private static readonly DateTimeOffset Ocorrido = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Dia = new(2026, 9, 1);

    private static Movimento Mov(TipoMovimento tipo, decimal valor, DateTimeOffset? quando = null, Guid? conta = null) =>
        Movimento.Criar(Guid.NewGuid(), conta ?? Conta, tipo, valor, quando ?? Ocorrido, DateTimeOffset.UtcNow);

    [Fact]
    public void Abrir_ComecaZerado()
    {
        var saldo = SaldoDiario.Abrir(Conta, Dia);

        Assert.Equal(0m, saldo.TotalCreditos);
        Assert.Equal(0m, saldo.TotalDebitos);
        Assert.Equal(0m, saldo.Saldo);
    }

    [Fact]
    public void Aplicar_CreditosEDebitos_AcumulaCorretamente()
    {
        var saldo = SaldoDiario.Abrir(Conta, Dia);

        saldo.Aplicar(Mov(TipoMovimento.Credito, 100m));
        saldo.Aplicar(Mov(TipoMovimento.Credito, 50.25m));
        saldo.Aplicar(Mov(TipoMovimento.Debito, 30m));

        Assert.Equal(150.25m, saldo.TotalCreditos);
        Assert.Equal(30m, saldo.TotalDebitos);
        Assert.Equal(120.25m, saldo.Saldo);
    }

    [Fact]
    public void Aplicar_SaldoPodeFicarNegativo()
    {
        var saldo = SaldoDiario.Abrir(Conta, Dia);
        saldo.Aplicar(Mov(TipoMovimento.Debito, 10m));
        Assert.Equal(-10m, saldo.Saldo);
    }

    [Fact]
    public void Aplicar_MovimentoDeOutraConta_Lanca()
    {
        var saldo = SaldoDiario.Abrir(Conta, Dia);
        Assert.Throws<DomainException>(() => saldo.Aplicar(Mov(TipoMovimento.Credito, 1m, conta: Guid.NewGuid())));
    }

    [Fact]
    public void Aplicar_MovimentoDeOutroDia_Lanca()
    {
        var saldo = SaldoDiario.Abrir(Conta, Dia);
        Assert.Throws<DomainException>(() => saldo.Aplicar(Mov(TipoMovimento.Credito, 1m, Ocorrido.AddDays(1))));
    }

    [Fact]
    public void Aplicar_Nulo_Lanca() =>
        Assert.Throws<ArgumentNullException>(() => SaldoDiario.Abrir(Conta, Dia).Aplicar(null!));
}
