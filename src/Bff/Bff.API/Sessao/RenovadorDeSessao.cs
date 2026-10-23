using System.Globalization;
using Bff.API.Auth;
using Microsoft.AspNetCore.Authentication;

namespace Bff.API.Sessao;

public sealed class RenovadorDeSessao(ITokenProvider tokens, TimeProvider timeProvider, SessaoOptions options)
{
    public const string AccessToken = "access_token";
    public const string RefreshToken = "refresh_token";
    public const string ExpiresAt = "expires_at";

    public async Task<ResultadoRenovacao> RenovarSeNecessarioAsync(
        AuthenticationProperties propriedades, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(propriedades);

        var agora = timeProvider.GetUtcNow();
        var expira = LerExpiracao(propriedades);
        var margem = TimeSpan.FromSeconds(options.RenovarAntesDeExpirarSegundos);
        if (expira is not null && expira.Value - agora > margem)
            return ResultadoRenovacao.NaoPrecisa;

        var refresh = propriedades.GetTokenValue(RefreshToken);
        if (string.IsNullOrEmpty(refresh))
            return ResultadoRenovacao.SessaoEncerrada;

        try
        {
            var novo = await tokens.RenovarAsync(refresh, cancellationToken);
            Atualizar(propriedades, novo, agora);
            return ResultadoRenovacao.Renovado;
        }
        catch (CredenciaisInvalidasException)
        {
            return ResultadoRenovacao.SessaoEncerrada;
        }
        catch (IdentidadeIndisponivelException)
        {
            return expira is not null && expira.Value > agora
                ? ResultadoRenovacao.NaoPrecisa
                : ResultadoRenovacao.SessaoEncerrada;
        }
    }

    private static DateTimeOffset? LerExpiracao(AuthenticationProperties propriedades) =>
        DateTimeOffset.TryParse(
            propriedades.GetTokenValue(ExpiresAt), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var valor)
            ? valor
            : null;

    private static void Atualizar(AuthenticationProperties propriedades, TokenResponse novo, DateTimeOffset agora)
    {
        var tokensAtuais = propriedades.GetTokens().ToList();
        Definir(tokensAtuais, AccessToken, novo.AccessToken);
        if (!string.IsNullOrEmpty(novo.RefreshToken))
            Definir(tokensAtuais, RefreshToken, novo.RefreshToken);
        Definir(tokensAtuais, ExpiresAt, agora.AddSeconds(novo.ExpiresIn).ToString("o", CultureInfo.InvariantCulture));
        propriedades.StoreTokens(tokensAtuais);
    }

    private static void Definir(List<AuthenticationToken> lista, string nome, string valor)
    {
        lista.RemoveAll(t => t.Name == nome);
        lista.Add(new AuthenticationToken { Name = nome, Value = valor });
    }
}
