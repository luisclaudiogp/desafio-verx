using Consolidado.Domain.Entities;
using Verx.SharedKernel;

namespace Consolidado.UnitTests.Domain;

public class MovimentoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 2, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Criar_Valido_NormalizaParaUtcEExpoeODia()
    {
        var ocorrido = new DateTimeOffset(2026, 9, 1, 22, 30, 0, TimeSpan.FromHours(-3));

        var m = Movimento.Criar(Guid.NewGuid(), Guid.NewGuid(), TipoMovimento.Credito, 10m, ocorrido, Agora);

        Assert.Equal(TimeSpan.Zero, m.OcorridoEm.Offset);
        Assert.Equal(new DateOnly(2026, 9, 2), m.Dia);
    }

    [Fact]
    public void Criar_IdsVazios_Lanca()
    {
        Assert.Throws<DomainException>(() => Movimento.Criar(Guid.Empty, Guid.NewGuid(), TipoMovimento.Credito, 1m, Agora, Agora));
        Assert.Throws<DomainException>(() => Movimento.Criar(Guid.NewGuid(), Guid.Empty, TipoMovimento.Credito, 1m, Agora, Agora));
    }

    [Fact]
    public void Criar_TipoInvalido_Lanca() =>
        Assert.Throws<DomainException>(() => Movimento.Criar(Guid.NewGuid(), Guid.NewGuid(), 0, 1m, Agora, Agora));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_ValorInvalido_Lanca(double valor) =>
        Assert.Throws<DomainException>(() =>
            Movimento.Criar(Guid.NewGuid(), Guid.NewGuid(), TipoMovimento.Credito, (decimal)valor, Agora, Agora));
}
