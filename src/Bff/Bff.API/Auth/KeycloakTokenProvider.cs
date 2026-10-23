using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Polly;

namespace Bff.API.Auth;

public sealed class KeycloakTokenProvider(HttpClient http, IOptions<KeycloakOptions> options) : ITokenProvider
{
    private readonly KeycloakOptions _options = options.Value;

    public Task<TokenResponse> LoginAsync(string usuario, string senha, CancellationToken cancellationToken) =>
        SolicitarTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = usuario,
                ["password"] = senha
            },
            cancellationToken);

    public Task<TokenResponse> RenovarAsync(string refreshToken, CancellationToken cancellationToken) =>
        SolicitarTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            },
            cancellationToken);

    public async Task EncerrarAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var resposta = await EnviarAsync(
            _options.LogoutEndpoint,
            new Dictionary<string, string> { ["refresh_token"] = refreshToken },
            cancellationToken);

        if (resposta.StatusCode >= HttpStatusCode.InternalServerError)
            throw new IdentidadeIndisponivelException("Servico de identidade indisponivel.");
    }

    private async Task<TokenResponse> SolicitarTokenAsync(
        Dictionary<string, string> campos, CancellationToken cancellationToken)
    {
        using var resposta = await EnviarAsync(_options.TokenEndpoint, campos, cancellationToken);

        if (resposta.IsSuccessStatusCode)
        {
            return await resposta.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                   ?? throw new IdentidadeIndisponivelException("Resposta de token invalida do servico de identidade.");
        }

        if (resposta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            throw new CredenciaisInvalidasException("Credenciais ou token invalidos.");

        throw new IdentidadeIndisponivelException("Servico de identidade indisponivel.");
    }

    private async Task<HttpResponseMessage> EnviarAsync(
        string endpoint, Dictionary<string, string> campos, CancellationToken cancellationToken)
    {
        campos["client_id"] = _options.ClientId;
        campos["client_secret"] = _options.ClientSecret;

        try
        {
            return await http.PostAsync(endpoint, new FormUrlEncodedContent(campos), cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new IdentidadeIndisponivelException("Servico de identidade indisponivel.", ex);
        }
        catch (ExecutionRejectedException ex)
        {
            throw new IdentidadeIndisponivelException("Servico de identidade indisponivel (timeout ou circuito aberto).", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IdentidadeIndisponivelException("Servico de identidade nao respondeu a tempo.", ex);
        }
    }
}
