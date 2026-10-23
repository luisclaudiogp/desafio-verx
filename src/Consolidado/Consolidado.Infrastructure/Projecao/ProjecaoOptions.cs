namespace Consolidado.Infrastructure.Projecao;

public sealed class ProjecaoOptions
{
    public const string Section = "Projecao";

    public ModoDeProjecao Modo { get; set; } = ModoDeProjecao.Ef;
}
