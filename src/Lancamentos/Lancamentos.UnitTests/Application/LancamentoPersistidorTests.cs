using Lancamentos.Application;
using Lancamentos.Application.Abstractions;
using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Exceptions;
using Lancamentos.Domain.Repositories;
using Lancamentos.UnitTests.Domain;
using Moq;
using Verx.SharedKernel;

namespace Lancamentos.UnitTests.Application;

public class LancamentoPersistidorTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ILancamentoRepository> _repository = new();
    private readonly Mock<IPosicaoContaRepository> _posicoes = new();
    private readonly Mock<ILancamentoEventPublisher> _publisher = new();
    private readonly Mock<IVerificadorDeReplay> _replays = new();
    private readonly PassThroughUnitOfWork _uow = new();
    private readonly LancamentoPersistidor _sut;
    private readonly Lancamento _lancamento = Lancamento.Registrar(LancamentoTests.Dados(), Agora);

    public LancamentoPersistidorTests()
    {
        _posicoes.Setup(p => p.ObterParaAtualizacaoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid conta, CancellationToken _) => PosicaoConta.Abrir(conta));
        _sut = new LancamentoPersistidor(_repository.Object, _posicoes.Object, _publisher.Object, _uow, _replays.Object, TimeProvider.System);
    }

    [Fact]
    public async Task Persistir_Sucesso_AplicaNaPosicaoAdicionaEPublicaNaMesmaTransacao()
    {
        var resultado = await _sut.PersistirAsync(_lancamento, CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.False(resultado.Replay);
        Assert.Equal(_lancamento.Id, resultado.Lancamento.Id);
        Assert.Equal(1, _uow.Execucoes);
        _posicoes.Verify(p => p.ObterParaAtualizacaoAsync(_lancamento.ContaId, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.AdicionarAsync(_lancamento, It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(p => p.PublicarCriacaoAsync(_lancamento, It.IsAny<CancellationToken>()), Times.Once);
        _replays.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Persistir_DebitoSemSaldo_LancaSemAdicionarNemPublicar()
    {
        var debito = Lancamento.Registrar(LancamentoTests.Dados(TipoMovimento.Debito, 10m), Agora);

        await Assert.ThrowsAsync<SaldoInsuficienteException>(() => _sut.PersistirAsync(debito, CancellationToken.None));

        _repository.Verify(r => r.AdicionarAsync(It.IsAny<Lancamento>(), It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(p => p.PublicarCriacaoAsync(It.IsAny<Lancamento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Persistir_CorridaNaMesmaChave_DelegaAoVerificadorEDevolveOVencedor()
    {
        var vencedor = new ResultadoRegistro(LancamentoDto.De(_lancamento), Replay: true);
        _uow.FalhaAoConfirmar = new ChaveDuplicadaException();
        _replays.Setup(r => r.BuscarAsync(_lancamento.ContaId, _lancamento.ChaveIdempotencia, _lancamento.HashRequisicao, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vencedor);

        var resultado = await _sut.PersistirAsync(_lancamento, CancellationToken.None);

        Assert.Same(vencedor, resultado);
    }

    [Fact]
    public async Task Persistir_DuplicidadeSemVencedor_TentaDeNovoEDevolveNuloAoEsgotar()
    {
        _uow.FalhaAoConfirmar = new ChaveDuplicadaException();

        Assert.Null(await _sut.PersistirAsync(_lancamento, CancellationToken.None));
        Assert.Equal(5, _uow.Execucoes);
    }

    [Fact]
    public async Task Persistir_CorridaNaCriacaoDaPosicao_RepeteESucede()
    {
        _uow.FalhaAoConfirmar = new ChaveDuplicadaException();
        _uow.QuantidadeDeFalhas = 1;

        var resultado = await _sut.PersistirAsync(_lancamento, CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.False(resultado.Replay);
        Assert.Equal(2, _uow.Execucoes);
    }

    [Fact]
    public async Task Persistir_ConflitoOtimista_RepeteAteConseguir()
    {
        _uow.FalhaAoConfirmar = new ConcorrenciaException("xmin");
        _uow.QuantidadeDeFalhas = 2;

        var resultado = await _sut.PersistirAsync(_lancamento, CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal(3, _uow.Execucoes);
    }

    [Fact]
    public async Task Persistir_ConflitoOtimistaPersistente_PropagaAposAsTentativas()
    {
        _uow.FalhaAoConfirmar = new ConcorrenciaException("xmin");

        await Assert.ThrowsAsync<ConcorrenciaException>(() => _sut.PersistirAsync(_lancamento, CancellationToken.None));
        Assert.Equal(5, _uow.Execucoes);
    }

    [Fact]
    public async Task Persistir_CorridaComCorpoDiferente_PropagaChaveReutilizada()
    {
        _uow.FalhaAoConfirmar = new ChaveDuplicadaException();
        _replays.Setup(r => r.BuscarAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ChaveIdempotenciaReutilizadaException());

        await Assert.ThrowsAsync<ChaveIdempotenciaReutilizadaException>(
            () => _sut.PersistirAsync(_lancamento, CancellationToken.None));
    }
}
