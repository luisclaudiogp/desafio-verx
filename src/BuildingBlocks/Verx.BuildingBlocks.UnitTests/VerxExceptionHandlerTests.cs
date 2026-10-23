using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Verx.SharedKernel;
using Verx.Web.Errors;
using Verx.Web.Http;

namespace Verx.BuildingBlocks.UnitTests;

public class VerxExceptionHandlerTests
{
    private static async Task<(int Status, string Detail)> Tratar(Exception ex)
    {
        var contexto = new DefaultHttpContext();
        var corpo = new MemoryStream();
        contexto.Response.Body = corpo;

        var tratado = await new VerxExceptionHandler(NullLogger<VerxExceptionHandler>.Instance)
            .TryHandleAsync(contexto, ex, CancellationToken.None);

        Assert.True(tratado);
        corpo.Position = 0;
        using var json = await JsonDocument.ParseAsync(corpo);
        return (contexto.Response.StatusCode, json.RootElement.GetProperty("detail").GetString()!);
    }

    [Fact]
    public async Task NaoEncontrado_404ComMensagem()
    {
        var (status, detalhe) = await Tratar(new NaoEncontradoException("sumiu"));
        Assert.Equal(404, status);
        Assert.Equal("sumiu", detalhe);
    }

    [Fact]
    public async Task Dominio_422ComMensagem()
    {
        var (status, detalhe) = await Tratar(new DomainException("regra"));
        Assert.Equal(422, status);
        Assert.Equal("regra", detalhe);
    }

    [Fact]
    public async Task RequisicaoInvalida_400ComMensagem()
    {
        var (status, detalhe) = await Tratar(new RequisicaoInvalidaException("header faltando"));
        Assert.Equal(400, status);
        Assert.Equal("header faltando", detalhe);
    }

    [Fact]
    public async Task CorpoInvalido_400SemVazarDetalhesDoParser()
    {
        var (status, detalhe) = await Tratar(new BadHttpRequestException("detalhe interno do json"));
        Assert.Equal(400, status);
        Assert.DoesNotContain("interno", detalhe, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Concorrencia_409()
    {
        var (status, _) = await Tratar(new ConcorrenciaException());
        Assert.Equal(409, status);
    }

    [Fact]
    public async Task Inesperado_500SemVazarDetalhes()
    {
        var (status, detalhe) = await Tratar(new InvalidOperationException("segredo"));
        Assert.Equal(500, status);
        Assert.DoesNotContain("segredo", detalhe, StringComparison.Ordinal);
    }

    [Fact]
    public void IdempotencyKey_Presente_RetornaSemEspacos()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers["Idempotency-Key"] = "  abc  ";
        Assert.Equal("abc", IdempotencyKey.Obter(request));
    }

    [Fact]
    public void IdempotencyKey_Ausente_LancaRequisicaoInvalida() =>
        Assert.Throws<RequisicaoInvalidaException>(() => IdempotencyKey.Obter(new DefaultHttpContext().Request));
}
