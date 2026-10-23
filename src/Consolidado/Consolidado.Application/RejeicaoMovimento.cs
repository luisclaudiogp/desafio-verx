namespace Consolidado.Application;

public sealed record RejeicaoMovimento(Guid LancamentoId, string Payload, string Motivo, DateTimeOffset RejeitadoEm);
