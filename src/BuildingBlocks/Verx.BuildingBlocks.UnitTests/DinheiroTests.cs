using Verx.SharedKernel;

namespace Verx.BuildingBlocks.UnitTests;

public class DinheiroTests
{
    [Theory]
    [InlineData("0.01")]
    [InlineData("100")]
    [InlineData("1234.56")]
    [InlineData("999999999999.99")]
    public void Criar_ValorValido_Aceita(string valor) =>
        Assert.Equal(decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture),
            Dinheiro.Criar(decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)).Valor);

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    [InlineData("1000000000000")]
    public void Criar_ValorInvalido_Lanca(string valor) =>
        Assert.Throws<DomainException>(() => Dinheiro.Criar(decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Igualdade_PorValor() =>
        Assert.Equal(Dinheiro.Criar(10m), Dinheiro.Criar(10.00m));

    [Fact]
    public void ToString_UsaDuasCasasECulturaInvariante() =>
        Assert.Equal("10.50", Dinheiro.Criar(10.5m).ToString());
}
