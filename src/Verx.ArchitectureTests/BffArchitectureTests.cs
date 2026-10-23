using System.Reflection;
using NetArchTest.Rules;

namespace Verx.ArchitectureTests;

public class BffArchitectureTests
{
    private static readonly Assembly Bff = typeof(Bff.API.BffServiceCollectionExtensions).Assembly;

    [Theory]
    [InlineData("Lancamentos")]
    [InlineData("Consolidado")]
    [InlineData("Shared.Contracts")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("MassTransit")]
    public void Bff_NaoConheceRegrasDeNegocioNemPersistencia(string proibido)
    {
        var resultado = Types.InAssembly(Bff).ShouldNot().HaveDependencyOn(proibido).GetResult();

        Assert.True(resultado.IsSuccessful, "Violacoes: " + string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    [Fact]
    public void Servicos_NaoDependemDoBff()
    {
        var assemblies = new[]
        {
            typeof(Lancamentos.Domain.Entities.Lancamento).Assembly,
            typeof(Lancamentos.Application.LancamentoDto).Assembly,
            typeof(Lancamentos.Infrastructure.DependencyInjection).Assembly,
            typeof(Consolidado.Domain.Entities.SaldoDiario).Assembly,
            typeof(Consolidado.Application.SaldoDiarioDto).Assembly,
            typeof(Consolidado.Infrastructure.DependencyInjection).Assembly
        };

        var resultado = Types.InAssemblies(assemblies).ShouldNot().HaveDependencyOn("Bff").GetResult();

        Assert.True(resultado.IsSuccessful, "Violacoes: " + string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
