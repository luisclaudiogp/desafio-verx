using System.ComponentModel.DataAnnotations;

namespace Bff.API.Auth;

public sealed class KeycloakOptions
{
    public const string Secao = "Keycloak";

    [Required]
    public string TokenEndpoint { get; set; } = string.Empty;

    [Required]
    public string LogoutEndpoint { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;
}
