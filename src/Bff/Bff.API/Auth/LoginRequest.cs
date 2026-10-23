using System.ComponentModel.DataAnnotations;

namespace Bff.API.Auth;

public sealed class LoginRequest
{
    [Required, MaxLength(255)]
    public string Usuario { get; init; } = string.Empty;

    [Required, MaxLength(512)]
    public string Senha { get; init; } = string.Empty;
}
