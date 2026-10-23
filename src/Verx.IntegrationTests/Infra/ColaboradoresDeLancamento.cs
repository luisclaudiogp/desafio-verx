using Lancamentos.Application;
using Lancamentos.Application.Abstractions;
using Lancamentos.Domain.Repositories;
using Lancamentos.Infrastructure.Data;
using Lancamentos.Infrastructure.Repositories;
using Moq;
using Verx.Persistence;

namespace Verx.IntegrationTests.Infra;

public static class ColaboradoresDeLancamento
{
    public static (IVerificadorDeReplay Replays, ILancamentoPersistidor Persistidor) Criar(
        ModoDePosicao modo, LancamentosDbContext contexto)
    {
        var repository = new LancamentoRepository(contexto);
        var replays = new VerificadorDeReplay(repository);
        IPosicaoContaRepository posicoes = modo == ModoDePosicao.Sql
            ? new SqlPosicaoContaRepository(contexto)
            : new EfPosicaoContaRepository(contexto);

        var persistidor = new LancamentoPersistidor(
            repository, posicoes, Mock.Of<ILancamentoEventPublisher>(),
            new EfUnitOfWork<LancamentosDbContext>(contexto), replays, TimeProvider.System);
        return (replays, persistidor);
    }
}
