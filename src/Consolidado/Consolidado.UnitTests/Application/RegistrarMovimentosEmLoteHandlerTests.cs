using Consolidado.Application;
using Consolidado.Application.Abstractions;
using Consolidado.Application.Commands;
using Consolidado.Domain.Entities;
using Moq;
using Verx.SharedKernel;

namespace Consolidado.UnitTests.Application;

public class RegistrarMovimentosEmLoteHandlerTests
{
    private static readonly Guid ContaA = Guid.NewGuid();
    private static readonly Guid ContaB = Guid.NewGuid();
    private static readonly DateTimeOffset Ocorrido = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IProjecaoRepository> _projecao = new();
    private readonly Mock<IProjecaoTransacao> _transacao = new();
    private readonly Mock<ISaldoCacheInvalidator> _cache = new();
    private readonly Mock<ICorretorDeSnapshots> _snapshots = new();
    private readonly RegistrarMovimentosEmLoteHandler _handler;
    private IReadOnlyCollection<SaldoDiario>? _deltas;

    public RegistrarMovimentosEmLoteHandlerTests()
    {
        _projecao.Setup(p => p.IniciarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_transacao.Object);
        _transacao
            .Setup(t => t.InserirMovimentosAsync(It.IsAny<IReadOnlyList<Movimento>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Movimento> m, CancellationToken _) => m);
        _transacao
            .Setup(t => t.AcumularSaldosAsync(It.IsAny<IReadOnlyCollection<SaldoDiario>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<SaldoDiario>, CancellationToken>((d, _) => _deltas = d)
            .Returns(Task.CompletedTask);

        _handler = new RegistrarMovimentosEmLoteHandler(_projecao.Object, _snapshots.Object, _cache.Object, TimeProvider.System);
    }

    private static MovimentoRecebido Item(Guid conta, TipoMovimento tipo, decimal valor, DateTimeOffset? quando = null, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), conta, tipo, valor, quando ?? Ocorrido);

    private Task<ResultadoProjecao> Executar(params MovimentoRecebido[] itens) =>
        _handler.HandleAsync(new RegistrarMovimentosEmLoteCommand(itens), CancellationToken.None);

    [Fact]
    public async Task Lote_AgregaDeltasPorContaEDia()
    {
        var resultado = await Executar(
            Item(ContaA, TipoMovimento.Credito, 100m),
            Item(ContaA, TipoMovimento.Credito, 50m),
            Item(ContaA, TipoMovimento.Debito, 30m),
            Item(ContaB, TipoMovimento.Credito, 10m),
            Item(ContaA, TipoMovimento.Credito, 7m, Ocorrido.AddDays(1)));

        Assert.Equal(new ResultadoProjecao(5, 0, 0), resultado);
        Assert.NotNull(_deltas);
        Assert.Equal(3, _deltas.Count);

        var a = _deltas.Single(d => d.ContaId == ContaA && d.Data == new DateOnly(2026, 9, 1));
        Assert.Equal(150m, a.TotalCreditos);
        Assert.Equal(30m, a.TotalDebitos);
        Assert.Equal(10m, _deltas.Single(d => d.ContaId == ContaB).TotalCreditos);
        Assert.Equal(7m, _deltas.Single(d => d.Data == new DateOnly(2026, 9, 2)).TotalCreditos);
        _transacao.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Lote_CorrigeSnapshotsComOsMesmosDeltasAntesDoCommit()
    {
        IReadOnlyCollection<SaldoDiario>? corrigidos = null;
        var ordem = new List<string>();
        _snapshots
            .Setup(s => s.CorrigirAsync(It.IsAny<IReadOnlyCollection<SaldoDiario>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<SaldoDiario>, CancellationToken>((d, _) =>
            {
                corrigidos = d;
                ordem.Add("snapshots");
            })
            .Returns(Task.CompletedTask);
        _transacao.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => ordem.Add("commit"))
            .Returns(Task.CompletedTask);

        await Executar(Item(ContaA, TipoMovimento.Credito, 100m, Ocorrido.AddDays(-30)));

        Assert.Same(_deltas, corrigidos);
        Assert.Equal(["snapshots", "commit"], ordem);
    }

    [Fact]
    public async Task ConflitoTransitorio_RepeteOLoteEmNovaTransacao()
    {
        _transacao
            .SetupSequence(t => t.InserirMovimentosAsync(It.IsAny<IReadOnlyList<Movimento>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcorrenciaException("chave duplicada"))
            .Returns(Task.FromResult<IReadOnlyList<Movimento>>([Movimento.Criar(Guid.NewGuid(), ContaA, TipoMovimento.Credito, 10m, Ocorrido, Ocorrido)]));

        var resultado = await Executar(Item(ContaA, TipoMovimento.Credito, 10m));

        Assert.Equal(new ResultadoProjecao(1, 0, 0), resultado);
        _projecao.Verify(p => p.IniciarAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _transacao.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConflitoPersistente_EsgotaAsTentativasEPropaga()
    {
        _transacao
            .Setup(t => t.InserirMovimentosAsync(It.IsAny<IReadOnlyList<Movimento>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcorrenciaException("chave duplicada"));

        await Assert.ThrowsAsync<ConcorrenciaException>(() => Executar(Item(ContaA, TipoMovimento.Credito, 10m)));

        _projecao.Verify(p => p.IniciarAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
        _transacao.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Duplicados_NaoEntramNosDeltas()
    {
        var novo = Item(ContaA, TipoMovimento.Credito, 100m);
        var repetido = Item(ContaA, TipoMovimento.Credito, 999m);
        _transacao
            .Setup(t => t.InserirMovimentosAsync(It.IsAny<IReadOnlyList<Movimento>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Movimento> m, CancellationToken _) => m.Where(x => x.LancamentoId == novo.LancamentoId).ToList());

        var resultado = await Executar(novo, repetido);

        Assert.Equal(new ResultadoProjecao(1, 1, 0), resultado);
        Assert.Equal(100m, _deltas!.Single().TotalCreditos);
    }

    [Fact]
    public async Task MesmoEventoDuasVezesNoLote_ConsideradoUmaVez()
    {
        var id = Guid.NewGuid();

        var resultado = await Executar(
            Item(ContaA, TipoMovimento.Credito, 100m, id: id),
            Item(ContaA, TipoMovimento.Credito, 100m, id: id));

        Assert.Equal(1, resultado.Aplicados);
        Assert.Equal(100m, _deltas!.Single().TotalCreditos);
    }

    [Fact]
    public async Task ItemInvalido_VaiParaRejeicoesEOsDemaisSaoAplicados()
    {
        IReadOnlyCollection<RejeicaoMovimento>? rejeicoes = null;
        _projecao
            .Setup(p => p.RegistrarRejeicoesAsync(It.IsAny<IReadOnlyCollection<RejeicaoMovimento>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<RejeicaoMovimento>, CancellationToken>((r, _) => rejeicoes = r)
            .Returns(Task.CompletedTask);
        var invalido = Item(ContaA, TipoMovimento.Credito, -5m);

        var resultado = await Executar(invalido, Item(ContaA, TipoMovimento.Credito, 10m));

        Assert.Equal(new ResultadoProjecao(1, 0, 1), resultado);
        Assert.NotNull(rejeicoes);
        Assert.Equal(invalido.LancamentoId, rejeicoes.Single().LancamentoId);
        Assert.Contains("maior que zero", rejeicoes.Single().Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TodosInvalidos_NaoAbremTransacao()
    {
        var resultado = await Executar(Item(ContaA, TipoMovimento.Credito, 0m));

        Assert.Equal(new ResultadoProjecao(0, 0, 1), resultado);
        _projecao.Verify(p => p.IniciarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AposCommit_InvalidaCacheDeCadaContaEDiaAfetado()
    {
        await Executar(
            Item(ContaA, TipoMovimento.Credito, 1m),
            Item(ContaA, TipoMovimento.Debito, 1m),
            Item(ContaB, TipoMovimento.Credito, 1m));

        _cache.Verify(c => c.InvalidarSaldoDiarioAsync(ContaA, new DateOnly(2026, 9, 1), It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.InvalidarSaldoDiarioAsync(ContaB, new DateOnly(2026, 9, 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FalhaNoCommit_PropagaSemInvalidarCache()
    {
        _transacao.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("banco fora"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Executar(Item(ContaA, TipoMovimento.Credito, 1m)));

        _cache.Verify(c => c.InvalidarSaldoDiarioAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
