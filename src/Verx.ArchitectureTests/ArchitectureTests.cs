using System.Reflection;
using NetArchTest.Rules;

namespace Verx.ArchitectureTests;

public class ArchitectureTests
{
    private static readonly Assembly LancDomain = typeof(Lancamentos.Domain.Entities.Lancamento).Assembly;
    private static readonly Assembly LancApplication = typeof(Lancamentos.Application.LancamentoDto).Assembly;
    private static readonly Assembly LancInfra = typeof(Lancamentos.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly ConsDomain = typeof(Consolidado.Domain.Entities.SaldoDiario).Assembly;
    private static readonly Assembly ConsApplication = typeof(Consolidado.Application.SaldoDiarioDto).Assembly;
    private static readonly Assembly ConsInfra = typeof(Consolidado.Infrastructure.DependencyInjection).Assembly;

    private static readonly string[] Infra =
    [
        "Microsoft.EntityFrameworkCore", "MassTransit", "Npgsql", "Microsoft.AspNetCore", "StackExchange.Redis", "RabbitMQ",
        "Shared.Contracts"
    ];

    public static TheoryData<string, Assembly> Dominios => new()
    {
        { "Lancamentos.Domain", LancDomain },
        { "Consolidado.Domain", ConsDomain }
    };

    public static TheoryData<string, Assembly, string> Aplicacoes => new()
    {
        { "Lancamentos.Application", LancApplication, "Lancamentos.Infrastructure" },
        { "Consolidado.Application", ConsApplication, "Consolidado.Infrastructure" }
    };

    [Theory]
    [MemberData(nameof(Dominios))]
    public void Dominio_NaoDependeDeInfraestruturaNemDeAplicacao(string nome, Assembly assembly)
    {
        var resultado = Types.InAssembly(assembly).ShouldNot()
            .HaveDependencyOnAny([.. Infra, nome.Replace("Domain", "Application", StringComparison.Ordinal),
                nome.Replace("Domain", "Infrastructure", StringComparison.Ordinal)])
            .GetResult();

        Assert.True(resultado.IsSuccessful, Falhas(resultado));
    }

    [Theory]
    [MemberData(nameof(Aplicacoes))]
    public void Aplicacao_NaoDependeDeInfraestrutura(string nome, Assembly assembly, string infraProjeto)
    {
        var resultado = Types.InAssembly(assembly).ShouldNot()
            .HaveDependencyOnAny([.. Infra, infraProjeto])
            .GetResult();

        Assert.True(resultado.IsSuccessful, $"{nome}: {Falhas(resultado)}");
    }

    [Fact]
    public void BoundedContexts_SaoIndependentes()
    {
        var lancamentos = Types.InAssemblies([LancDomain, LancApplication, LancInfra]).ShouldNot()
            .HaveDependencyOn("Consolidado").GetResult();
        var consolidado = Types.InAssemblies([ConsDomain, ConsApplication, ConsInfra]).ShouldNot()
            .HaveDependencyOn("Lancamentos").GetResult();

        Assert.True(lancamentos.IsSuccessful, Falhas(lancamentos));
        Assert.True(consolidado.IsSuccessful, Falhas(consolidado));
    }

    [Fact]
    public void Handlers_ResidemNaCamadaDeAplicacao()
    {
        var resultado = Types.InAssemblies([LancApplication, ConsApplication])
            .That().HaveNameEndingWith("Handler").Should().BeClasses().And().BeSealed().GetResult();

        Assert.True(resultado.IsSuccessful, Falhas(resultado));
    }

    [Fact]
    public void Entidades_NaoExpoemSettersPublicos()
    {
        var tipos = new[] { LancDomain, ConsDomain }
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Namespace?.EndsWith(".Entities", StringComparison.Ordinal) == true);

        var violacoes = tipos
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance), (t, p) => (t, p))
            .Where(x => x.p.SetMethod is { IsPublic: true } setter
                        && !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)))
            .Select(x => $"{x.t.Name}.{x.p.Name}")
            .ToList();

        Assert.True(violacoes.Count == 0, "Setters publicos em entidades: " + string.Join(", ", violacoes));
    }

    private static string Falhas(TestResult resultado) =>
        resultado.FailingTypeNames is null ? string.Empty : "Violacoes: " + string.Join(", ", resultado.FailingTypeNames);
}
