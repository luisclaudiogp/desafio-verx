using Lancamentos.Application;
using Lancamentos.Application.Abstractions;
using Lancamentos.Application.Commands;
using Lancamentos.Domain.Entities;
using Lancamentos.UnitTests.Domain;
using Moq;
using Verx.SharedKernel;

namespace Lancamentos.UnitTests.Application;

public class RegistrarLancamentoHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Conta = Guid.NewGuid();

    private readonly Mock<IVerificadorDeReplay> _replays = new();
    private readonly Mock<ILancamentoPersistidor> _persistidor = new();
    private readonly RegistrarLancamentoHandler _handler;

    public RegistrarLancamentoHandlerTests()
    {
        _handler = new RegistrarLancamentoHandler(_replays.Object, _persistidor.Object, new FixedTimeProvider(Agora));
    }

    private static RegistrarLancamentoCommand Comando(decimal valor = 100m) =>
        new(Conta, TipoMovimento.Credito, valor, null, "k1", "user-1");

    private static ResultadoRegistro Resultado(bool replay) =>
        new(LancamentoDto.De(Lancamento.Registrar(LancamentoTests.Dados(), Agora)), replay);

    [Fact]
    public async Task Handle_NovoLancamento_PersisteERetornaResultado()
    {
        var esperado = Resultado(false);
        _persistidor.Setup(r => r.PersistirAsync(It.IsAny<Lancamento>(), It.IsAny<CancellationToken>())).ReturnsAsync(esperado);

        var resultado = await _handler.HandleAsync(Comando(), CancellationToken.None);

        Assert.Same(esperado, resultado);
        _persistidor.Verify(r => r.PersistirAsync(
            It.Is<Lancamento>(l => l.ContaId == Conta && l.Valor.Valor == 100m && l.RegistradoPor == "user-1" && l.RegistradoEm == Agora),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ChaveJaUtilizada_RetornaReplaySemPersistir()
    {
        var replay = Resultado(true);
        _replays.Setup(r => r.BuscarAsync(Conta, "k1", HashDaRequisicao.DoRegistro(TipoMovimento.Credito, 100m, null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(replay);

        var resultado = await _handler.HandleAsync(Comando(), CancellationToken.None);

        Assert.Same(replay, resultado);
        _persistidor.Verify(r => r.PersistirAsync(It.IsAny<Lancamento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ConflitoSemVencedor_LancaConcorrenciaParaNovaTentativa()
    {
        _persistidor.Setup(r => r.PersistirAsync(It.IsAny<Lancamento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResultadoRegistro?)null);

        await Assert.ThrowsAsync<ConcorrenciaException>(() => _handler.HandleAsync(Comando(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValorInvalido_LancaSemPersistir()
    {
        await Assert.ThrowsAsync<DomainException>(() => _handler.HandleAsync(Comando(-1m), CancellationToken.None));

        _persistidor.Verify(r => r.PersistirAsync(It.IsAny<Lancamento>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
