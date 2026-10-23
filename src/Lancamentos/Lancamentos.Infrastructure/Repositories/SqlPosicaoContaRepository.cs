using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Repositories;
using Lancamentos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Lancamentos.Infrastructure.Repositories;

public sealed class SqlPosicaoContaRepository(LancamentosDbContext context) : IPosicaoContaRepository
{
    public async Task<PosicaoConta> ObterParaAtualizacaoAsync(Guid contaId, CancellationToken cancellationToken)
    {
        var posicao = await BloquearAsync(contaId, cancellationToken);
        if (posicao is not null)
            return posicao;

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO posicoes_contas ("ContaId", "Saldo") VALUES ({contaId}, 0) ON CONFLICT DO NOTHING""",
            cancellationToken);

        return await BloquearAsync(contaId, cancellationToken)
               ?? throw new InvalidOperationException("Nao foi possivel obter a posicao da conta.");
    }

    private async Task<PosicaoConta?> BloquearAsync(Guid contaId, CancellationToken cancellationToken)
    {
        var linhas = await context.Posicoes
            .FromSql($"""SELECT "ContaId", "Saldo", xmin FROM posicoes_contas WHERE "ContaId" = {contaId} FOR UPDATE""")
            .AsTracking()
            .ToListAsync(cancellationToken);

        return linhas.FirstOrDefault();
    }
}
