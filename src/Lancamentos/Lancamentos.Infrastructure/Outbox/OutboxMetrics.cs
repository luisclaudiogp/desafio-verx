using System.Diagnostics.Metrics;

namespace Lancamentos.Infrastructure.Outbox;

public static class OutboxMetrics
{
    public const string MeterName = "Verx.Outbox";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> Publicados = Meter.CreateCounter<long>("verx_outbox_publicados_total");

    public static readonly Counter<long> Falhas = Meter.CreateCounter<long>("verx_outbox_falhas_total");

    public static readonly Histogram<long> TamanhoDoLote = Meter.CreateHistogram<long>("verx_outbox_tamanho_lote");
}
