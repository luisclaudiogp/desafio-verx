using Bff.API.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bff.UnitTests;

public class IdentidadeExceptionHandlerTests
{
    private static async Task<(bool Tratado, int Status)> Tratar(Exception ex)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();

        var tratado = await new IdentidadeExceptionHandler(NullLogger<IdentidadeExceptionHandler>.Instance)
            .TryHandleAsync(contexto, ex, CancellationToken.None);

        return (tratado, contexto.Response.StatusCode);
    }

    [Fact]
    public async Task CredenciaisInvalidas_401()
    {
        var (tratado, status) = await Tratar(new CredenciaisInvalidasException("x"));

        Assert.True(tratado);
        Assert.Equal(401, status);
    }

    [Fact]
    public async Task IdentidadeIndisponivel_503()
    {
        var (tratado, status) = await Tratar(new IdentidadeIndisponivelException("x"));

        Assert.True(tratado);
        Assert.Equal(503, status);
    }

    [Fact]
    public async Task OutraExcecao_NaoTrata_DeixaParaOProximoHandler()
    {
        var (tratado, _) = await Tratar(new InvalidOperationException("x"));

        Assert.False(tratado);
    }
}
