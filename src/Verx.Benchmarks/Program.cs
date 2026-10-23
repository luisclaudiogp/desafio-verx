using System.Globalization;
using Consolidado.Infrastructure.Data;
using Consolidado.Infrastructure.Projecao;
using Lancamentos.Infrastructure.Repositories;
using Verx.Benchmarks;

var conexao = Argumento(args, "--conexao")
              ?? "Host=localhost;Port=5432;Username=postgres;Password=verx_dev_only;Gss Encryption Mode=Disable";
var repeticoes = int.Parse(Argumento(args, "--repeticoes") ?? "30", CultureInfo.InvariantCulture);
const string Todas = "todas";
var secao = Argumento(args, "--secao") ?? Todas;

Console.WriteLine($"Ambiente: {Environment.ProcessorCount} CPUs, .NET {Environment.Version}, PostgreSQL em {conexao.Split(';')[0]}.");

if (secao is Todas or "projecao")
    await ProjecaoAsync(conexao, repeticoes, args.Contains("--inverter"));

if (secao is Todas or "concorrencia")
    await ConcorrenciaAsync(conexao);

if (secao is Todas or "posicao")
    await PosicaoAsync(conexao);

static async Task ProjecaoAsync(string conexao, int repeticoes, bool inverter)
{
    CenarioDeCarga[] cenarios =
    [
        new("500 eventos, 10 contas (saldos já existem)", 500, 10, false, 0),
        new("500 eventos, 500 contas (saldos já existem)", 500, 500, false, 0),
        new("500 eventos, 500 contas novas (criam saldos)", 500, 500, true, 0),
        new("50 eventos, 50 contas (lote pequeno)", 50, 50, false, 0),
        new("500 eventos, 10 contas, 50% reenviados (duplicados)", 500, 10, false, 0.5)
    ];
    if (inverter)
        Array.Reverse(cenarios);

    Console.WriteLine();
    Console.WriteLine($"### Projeção do Consolidado: tempo para aplicar um lote (menor é melhor; {repeticoes} repetições, banco pré-aquecido, estratégias intercaladas)");
    Console.WriteLine();
    Console.WriteLine("| Cenário | Estratégia | Mediana (ms) | p95 (ms) | Eventos/s |");
    Console.WriteLine("|---|---|---:|---:|---:|");

    await using var banco = await BancoDeTeste.CriarAsync<ConsolidadoDbContext>(conexao, o => new ConsolidadoDbContext(o));
    var estrategias = Estrategias(banco.Fabrica);
    await AquecedorDeBanco.AquecerAsync(estrategias[0], lotes: 120);
    await AquecedorDeBanco.AquecerAsync(estrategias[1], lotes: 20);
    await AquecedorDeBanco.AquecerAsync(estrategias[2], lotes: 20);
    var medidor = new MedidorDeCenario(repeticoes, aquecimentos: 5);

    foreach (var cenario in cenarios)
    {
        var resultados = await medidor.MedirAsync(estrategias, cenario);
        foreach (var r in resultados)
        {
            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"| {cenario.Nome} | {r.Estrategia} | {r.MedianaMs:F1} | {r.P95Ms:F1} | {r.EventosPorSegundo:F0} |"));
        }
    }
}

static async Task ConcorrenciaAsync(string conexao)
{
    Console.WriteLine();
    Console.WriteLine("### Projeção sob concorrência: 8 trabalhadores, mesmas contas novas, 40 rodadas (320 lotes)");
    Console.WriteLine();
    Console.WriteLine("| Estratégia | Lotes | Falhas definitivas | Repetições extras | Principal falha | Saldos = movimentos? |");
    Console.WriteLine("|---|---:|---:|---:|---|---|");

    for (var i = 0; i < 3; i++)
    {
        await using var banco = await BancoDeTeste.CriarAsync<ConsolidadoDbContext>(conexao, o => new ConsolidadoDbContext(o));
        var estrategia = Estrategias(banco.Fabrica)[i];
        var r = await new ExperimentoDeConcorrencia(rodadas: 40, trabalhadores: 8, contasPorRodada: 20, eventosPorLote: 100)
            .ExecutarAsync(estrategia, banco.Fabrica);

        var motivo = r.MotivoMaisComum is null ? "—" : r.MotivoMaisComum.Replace("|", "/", StringComparison.Ordinal);
        Console.WriteLine($"| {r.Estrategia} | {r.Lotes} | {r.Falhas} | {r.TentativasExtras} | {motivo} | {(r.Consistente ? "sim" : "NÃO")} |");
    }
}

static async Task PosicaoAsync(string conexao)
{
    Console.WriteLine();
    Console.WriteLine("### Registro de lançamentos (posição de saldo): 2.000 operações, 32 em paralelo");
    Console.WriteLine();
    Console.WriteLine("| Contas | Modo | Ops/s | p50 (ms) | p95 (ms) | Aceitas | Saldo insuficiente | Conflitos (409) | Posição = ledger? |");
    Console.WriteLine("|---:|---|---:|---:|---:|---:|---:|---:|---|");

    foreach (var contas in new[] { 200, 10, 1 })
    {
        foreach (var modo in new[] { ModoDePosicao.Ef, ModoDePosicao.Sql })
        {
            var r = await new ExperimentoDePosicao(operacoes: 2000, contas, paralelismo: 32).ExecutarAsync(modo, conexao);
            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"| {r.Contas} | {r.Modo} | {r.OperacoesPorSegundo:F0} | {r.P50Ms:F1} | {r.P95Ms:F1} | {r.Aceitas} | {r.SaldoInsuficiente} | {r.Conflitos} | {(r.Consistente ? "sim" : "NÃO")} |"));
        }
    }
}

static IEstrategiaDeProjecao[] Estrategias(Func<ConsolidadoDbContext> fabrica) =>
[
    new ProjecaoPeloHandlerEstrategia(fabrica, ModoDeProjecao.Sql, "SQL: upsert em lote (opcional)"),
    new ProjecaoPeloHandlerEstrategia(fabrica, ModoDeProjecao.Ef, "EF com tratamento de conflito (padrão)"),
    new EfPuroEstrategia(fabrica)
];

static string? Argumento(string[] argumentos, string nome)
{
    var indice = Array.IndexOf(argumentos, nome);
    return indice >= 0 && indice + 1 < argumentos.Length ? argumentos[indice + 1] : null;
}
