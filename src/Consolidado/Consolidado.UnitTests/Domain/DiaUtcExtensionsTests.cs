using Consolidado.Domain.Tempo;

namespace Consolidado.UnitTests.Domain;

public class DiaUtcExtensionsTests
{
    [Fact]
    public void DiaUtc_ConverteOffsetNegativoParaODiaSeguinteEmUtc() =>
        Assert.Equal(new DateOnly(2026, 9, 10), new DateTimeOffset(2026, 9, 9, 22, 0, 0, TimeSpan.FromHours(-3)).DiaUtc());

    [Fact]
    public void DiaUtc_MantemODiaQuandoJaEstaEmUtc() =>
        Assert.Equal(new DateOnly(2026, 9, 9), new DateTimeOffset(2026, 9, 9, 23, 59, 59, TimeSpan.Zero).DiaUtc());

    [Fact]
    public void InicioDoDiaUtc_EMeiaNoiteComOffsetZero()
    {
        var inicio = new DateOnly(2026, 9, 9).InicioDoDiaUtc();

        Assert.Equal(new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero), inicio);
        Assert.Equal(TimeSpan.Zero, inicio.Offset);
    }
}
