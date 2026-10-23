namespace Verx.Benchmarks;

public sealed record ResultadoDeConcorrencia(
    string Estrategia, int Lotes, int Falhas, long TentativasExtras, string? MotivoMaisComum, bool Consistente, double DuracaoMs);
