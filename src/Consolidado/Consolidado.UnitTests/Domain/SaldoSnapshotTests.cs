using Consolidado.Domain.Entities;
using Verx.SharedKernel;

namespace Consolidado.UnitTests.Domain;

public class SaldoSnapshotTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 2, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Criar_PreencheCamposECalculaSaldo()
    {
        var conta = Guid.NewGuid();

        var s = SaldoSnapshot.Criar(conta, new DateOnly(2026, 9, 1), 150m, 30m, Agora.ToOffset(TimeSpan.FromHours(-3)));

        Assert.Equal(conta, s.ContaId);
        Assert.Equal(new DateOnly(2026, 9, 1), s.Data);
        Assert.Equal(120m, s.Saldo);
        Assert.Equal(Agora, s.GeradoEm);
        Assert.Equal(TimeSpan.Zero, s.GeradoEm.Offset);
    }

    [Fact]
    public void Criar_ContaVazia_Lanca() =>
        Assert.Throws<DomainException>(() => SaldoSnapshot.Criar(Guid.Empty, new DateOnly(2026, 9, 1), 0m, 0m, Agora));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Criar_TotaisNegativos_Lanca(int creditos, int debitos) =>
        Assert.Throws<DomainException>(() =>
            SaldoSnapshot.Criar(Guid.NewGuid(), new DateOnly(2026, 9, 1), creditos, debitos, Agora));
}
