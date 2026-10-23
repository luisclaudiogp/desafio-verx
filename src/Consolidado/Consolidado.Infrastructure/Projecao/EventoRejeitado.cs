namespace Consolidado.Infrastructure.Projecao;

public sealed class EventoRejeitado
{
    public Guid Id { get; private set; }
    public Guid LancamentoId { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public string Motivo { get; private set; } = string.Empty;
    public DateTimeOffset RejeitadoEm { get; private set; }

    private EventoRejeitado() { }

    public EventoRejeitado(Guid lancamentoId, string payload, string motivo, DateTimeOffset rejeitadoEm)
    {
        Id = Guid.NewGuid();
        LancamentoId = lancamentoId;
        Payload = payload;
        Motivo = motivo;
        RejeitadoEm = rejeitadoEm;
    }
}
