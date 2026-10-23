namespace Lancamentos.Infrastructure.Outbox;

public sealed class OutboxEvento
{
    public Guid Id { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; private set; }

    private OutboxEvento() { }

    public OutboxEvento(Guid id, string payload, DateTimeOffset criadoEm)
    {
        Id = id;
        Payload = payload;
        CriadoEm = criadoEm;
    }
}
