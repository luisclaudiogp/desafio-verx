namespace Bff.API.Auth;

public sealed record SessaoDto(string Usuario, IReadOnlyList<Guid> Contas);
