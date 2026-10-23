namespace Verx.Benchmarks;

public sealed record ResultadoDePosicao(
    string Modo,
    int Contas,
    int Operacoes,
    double OperacoesPorSegundo,
    double P50Ms,
    double P95Ms,
    int Aceitas,
    int SaldoInsuficiente,
    int Conflitos,
    bool Consistente);
