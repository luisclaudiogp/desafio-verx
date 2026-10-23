using Consolidado.Application.Abstractions;
using Consolidado.Application.Commands;
using Moq;
using Verx.SharedKernel;

namespace Consolidado.UnitTests.Application;

public class GerarSnapshotsHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);

    private readonly Mock<ISnapshotRepository> _snapshots = new();
    private readonly GerarSnapshotsHandler _handler;

    public GerarSnapshotsHandlerTests()
    {
        _handler = new GerarSnapshotsHandler(_snapshots.Object, new FixedTimeProvider(Agora));
    }

    [Fact]
    public async Task Handle_RepeteOsLotesAteNaoHaverMais()
    {
        _snapshots.SetupSequence(s => s.GerarLoteAsync(It.IsAny<DateOnly>(), 500, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(500)
            .ReturnsAsync(120)
            .ReturnsAsync(0);

        var resultado = await _handler.HandleAsync(new GerarSnapshotsCommand(1, 500), CancellationToken.None);

        Assert.Equal(620, resultado.Gerados);
        Assert.Equal(3, resultado.Lotes);
    }

    [Fact]
    public async Task Handle_GeraSomenteDiasEncerradosSegundoAMargem()
    {
        await _handler.HandleAsync(new GerarSnapshotsCommand(2, 100), CancellationToken.None);

        _snapshots.Verify(s => s.GerarLoteAsync(new DateOnly(2026, 9, 8), 100, Agora, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_LoteInvalido_LancaSemTocarNoRepositorio()
    {
        await Assert.ThrowsAsync<DomainException>(() => _handler.HandleAsync(new GerarSnapshotsCommand(1, 0), CancellationToken.None));

        _snapshots.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_MargemInvalida_Lanca() =>
        await Assert.ThrowsAsync<DomainException>(() => _handler.HandleAsync(new GerarSnapshotsCommand(0, 10), CancellationToken.None));
}
