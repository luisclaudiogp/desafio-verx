namespace Consolidado.Infrastructure.Messaging;

public sealed class MessagingOptions
{
    public const string Section = "Messaging";

    public ushort PrefetchCount { get; set; } = 2000;
    public int ConcorrenciaMaxima { get; set; } = 8;

    public int LoteTamanho { get; set; } = 500;
    public int LoteJanelaMs { get; set; } = 200;
    public int LoteConcorrencia { get; set; } = 4;

    public int RetryTentativas { get; set; } = 6;
    public int RetryMinimoMs { get; set; } = 200;
    public int RetryMaximoSegundos { get; set; } = 30;
    public int RetryDeltaSegundos { get; set; } = 1;

    public int CircuitBreakerJanelaSegundos { get; set; } = 60;
    public int CircuitBreakerFalhasParaAbrir { get; set; } = 15;
    public int CircuitBreakerMinimoAtivo { get; set; } = 10;
    public int CircuitBreakerResetSegundos { get; set; } = 60;
}
