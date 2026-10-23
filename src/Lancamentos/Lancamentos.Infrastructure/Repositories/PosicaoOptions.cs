namespace Lancamentos.Infrastructure.Repositories;

public sealed class PosicaoOptions
{
    public const string Section = "Posicao";

    public ModoDePosicao Modo { get; set; } = ModoDePosicao.Ef;
}
