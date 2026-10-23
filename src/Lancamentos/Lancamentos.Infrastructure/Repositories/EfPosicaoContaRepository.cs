using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Repositories;
using Lancamentos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Lancamentos.Infrastructure.Repositories;

public sealed class EfPosicaoContaRepository(LancamentosDbContext context) : IPosicaoContaRepository
{
    public async Task<PosicaoConta> ObterParaAtualizacaoAsync(Guid contaId, CancellationToken cancellationToken)
    {
        var posicao = await context.Posicoes.FirstOrDefaultAsync(p => p.ContaId == contaId, cancellationToken);
        if (posicao is not null)
            return posicao;

        posicao = PosicaoConta.Abrir(contaId);
        context.Posicoes.Add(posicao);
        return posicao;
    }
}
