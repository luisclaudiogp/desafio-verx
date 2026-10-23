using Lancamentos.Domain.Entities;
using Shared.Contracts;

namespace Lancamentos.Infrastructure.Outbox;

public static class LancamentoEventMapper
{
    public static LancamentoCriadoEvent ParaEvento(Lancamento lancamento)
    {
        ArgumentNullException.ThrowIfNull(lancamento);

        return new LancamentoCriadoEvent(
            lancamento.Id,
            lancamento.ContaId,
            lancamento.Tipo,
            lancamento.Valor.Valor,
            lancamento.OcorridoEm,
            lancamento.RegistradoEm);
    }
}
