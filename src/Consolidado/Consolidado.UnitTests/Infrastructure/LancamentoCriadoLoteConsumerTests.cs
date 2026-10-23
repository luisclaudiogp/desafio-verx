using Consolidado.Application;
using Consolidado.Application.Commands;
using Consolidado.Infrastructure.Messaging;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shared.Contracts;
using Verx.SharedKernel;

namespace Consolidado.UnitTests.Infrastructure;

public class LancamentoCriadoLoteConsumerTests
{
    private readonly Mock<ICommandHandler<RegistrarMovimentosEmLoteCommand, ResultadoProjecao>> _handler = new();

    private static LancamentoCriadoEvent Evento(decimal valor = 12.34m) =>
        new(Guid.NewGuid(), Guid.NewGuid(), TipoMovimento.Debito, valor, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);

    private static ConsumeContext<Batch<LancamentoCriadoEvent>> Contexto(params LancamentoCriadoEvent[] eventos)
    {
        var mensagens = eventos.Select(e =>
        {
            var m = new Mock<ConsumeContext<LancamentoCriadoEvent>>();
            m.SetupGet(c => c.Message).Returns(e);
            return m.Object;
        }).ToList();

        var contexto = new Mock<ConsumeContext<Batch<LancamentoCriadoEvent>>>();
        contexto.SetupGet(c => c.Message).Returns(new FakeBatch<LancamentoCriadoEvent>(mensagens));
        contexto.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return contexto.Object;
    }

    private LancamentoCriadoLoteConsumer Criar() =>
        new(_handler.Object, NullLogger<LancamentoCriadoLoteConsumer>.Instance);

    [Fact]
    public async Task Consume_TraduzOLoteEmUmUnicoComando()
    {
        var a = Evento(1m);
        var b = Evento(2m);
        RegistrarMovimentosEmLoteCommand? recebido = null;
        _handler.Setup(h => h.HandleAsync(It.IsAny<RegistrarMovimentosEmLoteCommand>(), It.IsAny<CancellationToken>()))
            .Callback<RegistrarMovimentosEmLoteCommand, CancellationToken>((c, _) => recebido = c)
            .ReturnsAsync(new ResultadoProjecao(2, 0, 0));

        await Criar().Consume(Contexto(a, b));

        Assert.NotNull(recebido);
        Assert.Equal(2, recebido.Itens.Count);
        Assert.Equal(new MovimentoRecebido(a.LancamentoId, a.ContaId, a.Tipo, a.Valor, a.OcorridoEm), recebido.Itens[0]);
        Assert.Equal(2m, recebido.Itens[1].Valor);
        _handler.Verify(h => h.HandleAsync(It.IsAny<RegistrarMovimentosEmLoteCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_ComDuplicadosERejeitados_NaoLanca()
    {
        _handler.Setup(h => h.HandleAsync(It.IsAny<RegistrarMovimentosEmLoteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoProjecao(1, 2, 1));

        await Criar().Consume(Contexto(Evento()));
    }

    [Fact]
    public async Task Consume_FalhaTransitoria_PropagaParaORetry()
    {
        _handler.Setup(h => h.HandleAsync(It.IsAny<RegistrarMovimentosEmLoteCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("banco fora"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar().Consume(Contexto(Evento())));
    }

    [Fact]
    public async Task Consume_ContextoNulo_Lanca() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Criar().Consume(null!));
}
