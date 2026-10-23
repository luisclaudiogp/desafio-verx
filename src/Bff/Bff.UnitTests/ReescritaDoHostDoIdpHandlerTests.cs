using System.Net;
using Bff.API.Sessao;

namespace Bff.UnitTests;

public class ReescritaDoHostDoIdpHandlerTests
{
    private static async Task<Uri?> Enviar(string uriDaChamada)
    {
        var interno = StubHttpMessageHandler.Retornando(HttpStatusCode.OK);
        using var cliente = new HttpClient(new ReescritaDoHostDoIdpHandler("http://localhost:8081", "http://keycloak:8080")
        {
            InnerHandler = interno
        });

        await cliente.GetAsync(uriDaChamada);

        return interno.Requisicoes.Single().Uri;
    }

    [Fact]
    public async Task EnderecoPublicoDoIdp_VaiParaOEnderecoInterno() =>
        Assert.Equal(
            "http://keycloak:8080/realms/Verx/protocol/openid-connect/token",
            (await Enviar("http://localhost:8081/realms/Verx/protocol/openid-connect/token"))!.ToString());

    [Fact]
    public async Task QueryStringEPathSaoPreservados() =>
        Assert.Equal(
            "http://keycloak:8080/realms/Verx/protocol/openid-connect/certs?a=1&b=2",
            (await Enviar("http://localhost:8081/realms/Verx/protocol/openid-connect/certs?a=1&b=2"))!.ToString());

    [Fact]
    public async Task OutrosHosts_NaoSaoAlterados() =>
        Assert.Equal("http://exemplo.com:9000/x", (await Enviar("http://exemplo.com:9000/x"))!.ToString());

    [Fact]
    public async Task MesmoHostEmOutraPorta_NaoSofreReescrita() =>
        Assert.Equal("http://localhost:5000/x", (await Enviar("http://localhost:5000/x"))!.ToString());
}
