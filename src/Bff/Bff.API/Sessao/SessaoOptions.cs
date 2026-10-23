using System.ComponentModel.DataAnnotations;

namespace Bff.API.Sessao;

public sealed class SessaoOptions
{
    public const string Secao = "Sessao";

    public bool Habilitada { get; set; } = true;

    [Required]
    public string NomeDoCookie { get; set; } = "verx.sessao";

    [Range(1, 1440)]
    public int InatividadeMinutos { get; set; } = 30;

    [Range(0, 600)]
    public int RenovarAntesDeExpirarSegundos { get; set; } = 60;

    [Required]
    public string UrlPublicaDoIdp { get; set; } = "http://localhost:8081";

    [Required]
    public string UrlInternaDoIdp { get; set; } = "http://localhost:8081";

    [Required]
    public string Realm { get; set; } = "Verx";

    [Required]
    public string RedisConnectionString { get; set; } = "localhost:6379,abortConnect=false,connectTimeout=500,syncTimeout=500";

    public string Authority => $"{UrlPublicaDoIdp.TrimEnd('/')}/realms/{Realm}";
}
