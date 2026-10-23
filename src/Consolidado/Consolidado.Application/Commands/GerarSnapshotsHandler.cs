using Consolidado.Application.Abstractions;
using Consolidado.Domain.Snapshots;
using Verx.SharedKernel;

namespace Consolidado.Application.Commands;

public sealed class GerarSnapshotsHandler(
    ISnapshotRepository snapshots,
    TimeProvider timeProvider) : ICommandHandler<GerarSnapshotsCommand, ResultadoSnapshots>
{
    public async Task<ResultadoSnapshots> HandleAsync(GerarSnapshotsCommand command, CancellationToken cancellationToken)
    {
        if (command.LoteContas < 1)
            throw new DomainException("O tamanho do lote de contas deve ser positivo.");

        var agora = timeProvider.GetUtcNow();
        var ate = PoliticaDeSnapshot.DataLimite(agora, command.MargemDias);

        var total = 0;
        var lotes = 0;
        int gerados;
        do
        {
            gerados = await snapshots.GerarLoteAsync(ate, command.LoteContas, agora, cancellationToken);
            total += gerados;
            lotes++;
        }
        while (gerados > 0);

        return new ResultadoSnapshots(total, lotes);
    }
}
