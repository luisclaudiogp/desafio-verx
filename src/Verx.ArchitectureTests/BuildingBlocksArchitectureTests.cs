using NetArchTest.Rules;

namespace Verx.ArchitectureTests;

public class BuildingBlocksArchitectureTests
{
    private static readonly System.Reflection.Assembly Web = typeof(Verx.Web.RateLimiting.ITokenBucketStore).Assembly;
    private static readonly System.Reflection.Assembly Redis = typeof(Verx.Web.Redis.RedisTokenBucketStore).Assembly;
    private static readonly System.Reflection.Assembly Kernel = typeof(Verx.SharedKernel.Dinheiro).Assembly;

    [Theory]
    [InlineData("StackExchange.Redis")]
    [InlineData("Polly")]
    [InlineData("Lancamentos")]
    [InlineData("Consolidado")]
    [InlineData("Bff")]
    public void VerxWeb_ExpoePortasEnaoConheceAdaptadoresNemServicos(string proibido)
    {
        var resultado = Types.InAssembly(Web).ShouldNot().HaveDependencyOn(proibido).GetResult();

        Assert.True(resultado.IsSuccessful, "Violacoes: " + string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    [Fact]
    public void AdaptadorRedis_ImplementaAPortaDoVerxWeb()
    {
        var implementacoes = Types.InAssembly(Redis).That().ImplementInterface(typeof(Verx.Web.RateLimiting.ITokenBucketStore))
            .GetTypes().Select(t => t.Name).ToList();

        Assert.Contains("RedisTokenBucketStore", implementacoes);
        Assert.Contains("ResilientTokenBucketStore", implementacoes);
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("StackExchange.Redis")]
    [InlineData("MassTransit")]
    public void SharedKernel_NaoDependeDeFrameworksDeInfraestrutura(string proibido)
    {
        var resultado = Types.InAssembly(Kernel).ShouldNot().HaveDependencyOn(proibido).GetResult();

        Assert.True(resultado.IsSuccessful, "Violacoes: " + string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
