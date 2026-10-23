namespace Verx.Benchmarks;

public sealed record ResultadoDeCenario(string Estrategia, int Eventos, double MedianaMs, double P95Ms, double MinimoMs)
{
    public double EventosPorSegundo => Eventos / (MedianaMs / 1000d);
}
