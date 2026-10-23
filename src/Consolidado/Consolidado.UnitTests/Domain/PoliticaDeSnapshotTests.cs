using Consolidado.Domain.Snapshots;
using Verx.SharedKernel;

namespace Consolidado.UnitTests.Domain;

public class PoliticaDeSnapshotTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 10, 0, 30, 0, TimeSpan.Zero);

    [Fact]
    public void DataLimite_ComMargemDeUmDia_EhOntemEmUtc() =>
        Assert.Equal(new DateOnly(2026, 9, 9), PoliticaDeSnapshot.DataLimite(Agora, 1));

    [Fact]
    public void DataLimite_UsaOdiaUtcEMesmoComOffsetNegativo() =>
        Assert.Equal(new DateOnly(2026, 9, 9), PoliticaDeSnapshot.DataLimite(Agora.ToOffset(TimeSpan.FromHours(-3)), 1));

    [Fact]
    public void DataLimite_MargemMaior_RecuaMaisDias() =>
        Assert.Equal(new DateOnly(2026, 9, 7), PoliticaDeSnapshot.DataLimite(Agora, 3));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DataLimite_MargemInferiorAoMinimo_Lanca(int margem) =>
        Assert.Throws<DomainException>(() => PoliticaDeSnapshot.DataLimite(Agora, margem));
}
