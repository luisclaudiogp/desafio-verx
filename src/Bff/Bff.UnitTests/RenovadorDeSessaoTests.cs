using System.Globalization;
using Bff.API.Auth;
using Bff.API.Sessao;
using Microsoft.AspNetCore.Authentication;
using Moq;

namespace Bff.UnitTests;

public class RenovadorDeSessaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ITokenProvider> _tokens = new();
    private readonly RenovadorDeSessao _sut;

    public RenovadorDeSessaoTests()
    {
        _sut = new RenovadorDeSessao(_tokens.Object, new FixedTimeProvider(Agora), new SessaoOptions { RenovarAntesDeExpirarSegundos = 60 });
    }

    private static AuthenticationProperties Sessao(DateTimeOffset? expira, string? refresh = "refresh-antigo")
    {
        var tokens = new List<AuthenticationToken> { new() { Name = "access_token", Value = "acesso-antigo" } };
        if (refresh is not null)
            tokens.Add(new AuthenticationToken { Name = "refresh_token", Value = refresh });
        if (expira is not null)
            tokens.Add(new AuthenticationToken { Name = "expires_at", Value = expira.Value.ToString("o", CultureInfo.InvariantCulture) });

        var propriedades = new AuthenticationProperties();
        propriedades.StoreTokens(tokens);
        return propriedades;
    }

    [Fact]
    public async Task TokenValidoPorMaisDaMargem_NaoRenova()
    {
        var resultado = await _sut.RenovarSeNecessarioAsync(Sessao(Agora.AddMinutes(10)), CancellationToken.None);

        Assert.Equal(ResultadoRenovacao.NaoPrecisa, resultado);
        _tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TokenPertoDeExpirar_RenovaEAtualizaOsTokensDaSessao()
    {
        var propriedades = Sessao(Agora.AddSeconds(30));
        _tokens.Setup(t => t.RenovarAsync("refresh-antigo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenResponse("acesso-novo", "refresh-novo", 900, 1800, "Bearer"));

        var resultado = await _sut.RenovarSeNecessarioAsync(propriedades, CancellationToken.None);

        Assert.Equal(ResultadoRenovacao.Renovado, resultado);
        Assert.Equal("acesso-novo", propriedades.GetTokenValue("access_token"));
        Assert.Equal("refresh-novo", propriedades.GetTokenValue("refresh_token"));
        Assert.Equal(Agora.AddSeconds(900), DateTimeOffset.Parse(propriedades.GetTokenValue("expires_at")!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task RenovacaoSemNovoRefreshToken_MantemOAntigo()
    {
        var propriedades = Sessao(Agora.AddSeconds(5));
        _tokens.Setup(t => t.RenovarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenResponse("acesso-novo", null, 900, 0, "Bearer"));

        await _sut.RenovarSeNecessarioAsync(propriedades, CancellationToken.None);

        Assert.Equal("refresh-antigo", propriedades.GetTokenValue("refresh_token"));
    }

    [Fact]
    public async Task SemExpiracaoRegistrada_TentaRenovarPorSeguranca()
    {
        _tokens.Setup(t => t.RenovarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenResponse("a", "r", 900, 0, "Bearer"));

        var resultado = await _sut.RenovarSeNecessarioAsync(Sessao(null), CancellationToken.None);

        Assert.Equal(ResultadoRenovacao.Renovado, resultado);
    }

    [Fact]
    public async Task SemRefreshToken_EncerraASessao()
    {
        var resultado = await _sut.RenovarSeNecessarioAsync(Sessao(Agora.AddSeconds(5), refresh: null), CancellationToken.None);

        Assert.Equal(ResultadoRenovacao.SessaoEncerrada, resultado);
    }

    [Fact]
    public async Task RefreshRecusadoPeloIdp_EncerraASessao()
    {
        _tokens.Setup(t => t.RenovarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CredenciaisInvalidasException("expirou"));

        var resultado = await _sut.RenovarSeNecessarioAsync(Sessao(Agora.AddSeconds(5)), CancellationToken.None);

        Assert.Equal(ResultadoRenovacao.SessaoEncerrada, resultado);
    }

    [Fact]
    public async Task IdpIndisponivelMasTokenAindaValido_MantemASessao()
    {
        _tokens.Setup(t => t.RenovarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IdentidadeIndisponivelException("fora"));

        var resultado = await _sut.RenovarSeNecessarioAsync(Sessao(Agora.AddSeconds(20)), CancellationToken.None);

        Assert.Equal(ResultadoRenovacao.NaoPrecisa, resultado);
    }

    [Fact]
    public async Task IdpIndisponivelEToken_JaExpirado_EncerraASessao()
    {
        _tokens.Setup(t => t.RenovarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IdentidadeIndisponivelException("fora"));

        var resultado = await _sut.RenovarSeNecessarioAsync(Sessao(Agora.AddSeconds(-1)), CancellationToken.None);

        Assert.Equal(ResultadoRenovacao.SessaoEncerrada, resultado);
    }
}
