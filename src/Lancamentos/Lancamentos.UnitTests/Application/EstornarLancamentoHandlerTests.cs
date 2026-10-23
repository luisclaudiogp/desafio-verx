using Lancamentos.Application;
using Lancamentos.Application.Abstractions;
using Lancamentos.Application.Commands;
using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Repositories;
using Lancamentos.UnitTests.Domain;
using Moq;
using Verx.SharedKernel;

namespace Lancamentos.UnitTests.Application;

public class EstornarLancamentoHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ILancamentoRepository> _repository = new();
    private readonly Mock<IVerificadorDeReplay> _replays = new();
    private readonly Mock<ILancamentoPersistidor> _persistidor = new();
    private readonly EstornarLancamentoHandler _handler;
    private readonly Lancamento _original;
    private readonly Guid _conta;

    public EstornarLancamentoHandlerTests()
    {
        _handler = new EstornarLancamentoHandler(_repository.Object, _replays.Object, _persistidor.Object, new FixedTimeProvider(Agora));
        _conta = Guid.NewGuid();
        _original = Lancamento.Registrar(LancamentoTests.Dados(TipoMovimento.Debito, 40m, "orig", _conta), Agora);
    }

    private EstornarLancamentoCommand Comando(string chave = "est-1") => new(_conta, _original.Id, chave, "user-2");

    private void ExisteOriginal() =>
        _repository.Setup(r => r.ObterPorIdAsync(_conta, _original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_original);

    [Fact]
    public async Task Handle_LancamentoExistente_PersisteEstornoCompensatorio()
    {
        ExisteOriginal();
        var esperado = new ResultadoRegistro(LancamentoDto.De(_original.Estornar("est-1", "u", Agora)), false);
        _persistidor.Setup(r => r.PersistirAsync(It.IsAny<Lancamento>(), It.IsAny<CancellationToken>())).ReturnsAsync(esperado);

        var resultado = await _handler.HandleAsync(Comando(), CancellationToken.None);

        Assert.Same(esperado, resultado);
        _persistidor.Verify(r => r.PersistirAsync(
            It.Is<Lancamento>(l => l.Tipo == TipoMovimento.Credito && l.EstornoDeId == _original.Id && l.Valor.Valor == 40m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_LancamentoInexistente_LancaNaoEncontrado() =>
        await Assert.ThrowsAsync<NaoEncontradoException>(() => _handler.HandleAsync(Comando(), CancellationToken.None));

    [Fact]
    public async Task Handle_JaEstornado_Lanca()
    {
        ExisteOriginal();
        _repository.Setup(r => r.JaEstornadoAsync(_original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<DomainException>(() => _handler.HandleAsync(Comando(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MesmaChave_RetornaReplaySemBuscarOriginal()
    {
        var replay = new ResultadoRegistro(LancamentoDto.De(_original), true);
        _replays.Setup(r => r.BuscarAsync(_conta, "est-1", HashDaRequisicao.DoEstorno(_original.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(replay);

        var resultado = await _handler.HandleAsync(Comando(), CancellationToken.None);

        Assert.Same(replay, resultado);
        _repository.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CorridaComOutraChave_LancaJaEstornado()
    {
        ExisteOriginal();
        _persistidor.Setup(r => r.PersistirAsync(It.IsAny<Lancamento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResultadoRegistro?)null);

        await Assert.ThrowsAsync<DomainException>(() => _handler.HandleAsync(Comando(), CancellationToken.None));
    }
}
