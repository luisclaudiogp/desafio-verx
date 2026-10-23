namespace Lancamentos.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string Section = "Outbox";

    public int BatchSize { get; set; } = 500;
    public int Paralelismo { get; set; } = 2;
    public int PollOciosoMs { get; set; } = 50;
    public int BackoffMaximoMs { get; set; } = 5000;
}
