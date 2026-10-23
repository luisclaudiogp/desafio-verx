namespace Consolidado.Infrastructure.Snapshots;

public sealed class SnapshotOptions
{
    public const string Section = "Snapshots";

    public bool Habilitado { get; set; } = true;
    public int IntervaloSegundos { get; set; } = 600;
    public int MargemDias { get; set; } = 1;
    public int LoteContas { get; set; } = 500;
    public int RetentativaSegundos { get; set; } = 30;
}
