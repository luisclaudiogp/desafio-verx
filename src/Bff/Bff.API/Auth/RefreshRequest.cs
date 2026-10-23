using System.ComponentModel.DataAnnotations;

namespace Bff.API.Auth;

public sealed class RefreshRequest
{
    [Required, MaxLength(8192)]
    public string RefreshToken { get; init; } = string.Empty;
}
