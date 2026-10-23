using Verx.SharedKernel;

namespace Consolidado.Application.Commands;

public sealed record RegistrarMovimentosEmLoteCommand(IReadOnlyList<MovimentoRecebido> Itens) : ICommand<ResultadoProjecao>;
