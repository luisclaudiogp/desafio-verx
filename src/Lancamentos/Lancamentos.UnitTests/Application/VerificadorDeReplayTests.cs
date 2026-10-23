using Lancamentos.Application;
using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Exceptions;
using Lancamentos.Domain.Repositories;
using Lancamentos.UnitTests.Domain;
using Moq;
using Verx.SharedKernel;

namespace Lancamentos.UnitTests.Application;

public class VerificadorDeReplayTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ILancamentoRepository> _repository = new();
    private readonly VerificadorDeReplay _sut;
    private readonly Lancamento _lancamento = Lancamento.Registrar(LancamentoTests.Dados(), Agora);

    public VerificadorDeReplayTests()
    {
        _sut = new VerificadorDeReplay(_repository.Object);
    }

    [Fact]
    public async Task ChaveExistenteComMesmoCorpo_RetornaReplay()
    {
        _repository.Setup(r => r.ObterPorChaveAsync(_lancamento.ContaId, "k1", It.IsAny<CancellationToken>())).ReturnsAsync(_lancamento);

        var resultado = await _sut.BuscarAsync(_lancamento.ContaId, "k1", _lancamento.HashRequisicao, CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.True(resultado.Replay);
        Assert.Equal(_lancamento.Id, resultado.Lancamento.Id);
    }

    [Fact]
    public async Task ChaveExistenteComCorpoDiferente_LancaChaveReutilizada()
    {
        _repository.Setup(r => r.ObterPorChaveAsync(_lancamento.ContaId, "k1", It.IsAny<CancellationToken>())).ReturnsAsync(_lancamento);

        await Assert.ThrowsAsync<ChaveIdempotenciaReutilizadaException>(
            () => _sut.BuscarAsync(_lancamento.ContaId, "k1", HashDaRequisicao.DoRegistro(TipoMovimento.Debito, 1m, null), CancellationToken.None));
    }

    [Fact]
    public async Task ChaveInedita_RetornaNulo() =>
        Assert.Null(await _sut.BuscarAsync(Guid.NewGuid(), "nova", "hash", CancellationToken.None));
}
