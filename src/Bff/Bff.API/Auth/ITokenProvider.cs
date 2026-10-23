namespace Bff.API.Auth;

public interface ITokenProvider
{
    Task<TokenResponse> LoginAsync(string usuario, string senha, CancellationToken cancellationToken);

    Task<TokenResponse> RenovarAsync(string refreshToken, CancellationToken cancellationToken);

    Task EncerrarAsync(string refreshToken, CancellationToken cancellationToken);
}
