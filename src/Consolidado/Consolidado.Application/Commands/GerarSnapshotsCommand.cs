using Verx.SharedKernel;

namespace Consolidado.Application.Commands;

public sealed record GerarSnapshotsCommand(int MargemDias, int LoteContas) : ICommand<ResultadoSnapshots>;
