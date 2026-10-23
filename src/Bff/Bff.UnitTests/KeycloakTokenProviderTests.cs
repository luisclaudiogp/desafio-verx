using System.Net;
using Bff.API.Auth;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Bff.UnitTests;

public class KeycloakTokenProviderTests
{
    private const string TokenJson =
        """{"access_token":"acc","refresh_token":"ref","expires_in":900,"refresh_expires_in":1800,"token_type":"Bearer"}""";

    private static readonly KeycloakOptions Opcoes = new()
    {
        TokenEndpoint = "http://kc/token",
        LogoutEndpoint = "http://kc/logout",
        ClientId = "verx-bff",
        ClientSecret = "segredo"
    };

    private static KeycloakTokenProvider Criar(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler), Options.Create(Opcoes));

    [Fact]
    public async Task Login_Sucesso_DevolveTokensEEnviaCredenciaisDoClient()
    {
        var handler = StubHttpMessageHandler.Retornando(HttpStatusCode.OK, TokenJson);

        var token = await Criar(handler).LoginAsync("cliente.a", "s3nha", CancellationToken.None);

        Assert.Equal("acc", token.AccessToken);
        Assert.Equal("ref", token.RefreshToken);
        Assert.Equal(900, token.ExpiresIn);
        var (uri, corpo) = Assert.Single(handler.Requisicoes);
        Assert.Equal("http://kc/token", uri?.ToString());
        Assert.Contains("grant_type=password", corpo, StringComparison.Ordinal);
        Assert.Contains("username=cliente.a", corpo, StringComparison.Ordinal);
        Assert.Contains("client_id=verx-bff", corpo, StringComparison.Ordinal);
        Assert.Contains("client_secret=segredo", corpo, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Login_CredenciaisRecusadas_LancaCredenciaisInvalidas(HttpStatusCode status)
    {
        var provider = Criar(StubHttpMessageHandler.Retornando(status));

        await Assert.ThrowsAsync<CredenciaisInvalidasException>(
            () => provider.LoginAsync("x", "y", CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Login_KeycloakComErro_LancaIdentidadeIndisponivel(HttpStatusCode status)
    {
        var provider = Criar(StubHttpMessageHandler.Retornando(status));

        await Assert.ThrowsAsync<IdentidadeIndisponivelException>(
            () => provider.LoginAsync("x", "y", CancellationToken.None));
    }

    [Fact]
    public async Task Login_FalhaDeRede_LancaIdentidadeIndisponivel()
    {
        var provider = Criar(new StubHttpMessageHandler(_ => throw new HttpRequestException("sem rota")));

        await Assert.ThrowsAsync<IdentidadeIndisponivelException>(
            () => provider.LoginAsync("x", "y", CancellationToken.None));
    }

    [Fact]
    public async Task Login_Timeout_LancaIdentidadeIndisponivel()
    {
        var provider = Criar(new StubHttpMessageHandler(_ => throw new TaskCanceledException("timeout")));

        await Assert.ThrowsAsync<IdentidadeIndisponivelException>(
            () => provider.LoginAsync("x", "y", CancellationToken.None));
    }

    [Fact]
    public async Task Login_TimeoutDaPolitica_LancaIdentidadeIndisponivel()
    {
        var provider = Criar(new StubHttpMessageHandler(_ => throw new TimeoutRejectedException("timeout")));

        await Assert.ThrowsAsync<IdentidadeIndisponivelException>(
            () => provider.LoginAsync("x", "y", CancellationToken.None));
    }

    [Fact]
    public async Task Login_CircuitoAberto_LancaIdentidadeIndisponivel()
    {
        var provider = Criar(new StubHttpMessageHandler(_ => throw new BrokenCircuitException("aberto")));

        await Assert.ThrowsAsync<IdentidadeIndisponivelException>(
            () => provider.LoginAsync("x", "y", CancellationToken.None));
    }

    [Fact]
    public async Task Login_CancelamentoDoChamador_Propaga()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var provider = Criar(new StubHttpMessageHandler(_ => throw new TaskCanceledException("cancelado")));

        await Assert.ThrowsAsync<TaskCanceledException>(() => provider.LoginAsync("x", "y", cts.Token));
    }

    [Fact]
    public async Task Login_CorpoNulo_LancaIdentidadeIndisponivel()
    {
        var provider = Criar(StubHttpMessageHandler.Retornando(HttpStatusCode.OK, "null"));

        await Assert.ThrowsAsync<IdentidadeIndisponivelException>(
            () => provider.LoginAsync("x", "y", CancellationToken.None));
    }

    [Fact]
    public async Task Renovar_EnviaRefreshToken()
    {
        var handler = StubHttpMessageHandler.Retornando(HttpStatusCode.OK, TokenJson);

        await Criar(handler).RenovarAsync("meu-refresh", CancellationToken.None);

        var (_, corpo) = Assert.Single(handler.Requisicoes);
        Assert.Contains("grant_type=refresh_token", corpo, StringComparison.Ordinal);
        Assert.Contains("refresh_token=meu-refresh", corpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Encerrar_ChamaEndpointDeLogout()
    {
        var handler = StubHttpMessageHandler.Retornando(HttpStatusCode.NoContent);

        await Criar(handler).EncerrarAsync("meu-refresh", CancellationToken.None);

        var (uri, corpo) = Assert.Single(handler.Requisicoes);
        Assert.Equal("http://kc/logout", uri?.ToString());
        Assert.Contains("refresh_token=meu-refresh", corpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Encerrar_KeycloakComErro_LancaIdentidadeIndisponivel()
    {
        var provider = Criar(StubHttpMessageHandler.Retornando(HttpStatusCode.BadGateway));

        await Assert.ThrowsAsync<IdentidadeIndisponivelException>(
            () => provider.EncerrarAsync("r", CancellationToken.None));
    }
}
