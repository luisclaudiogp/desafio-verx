using Lancamentos.Infrastructure.Repositories;
using Verx.IntegrationTests.Infra;

namespace Verx.IntegrationTests;

[Collection(PostgresTestes.Nome)]
public sealed class LancamentosSaldoEfTests(PostgresFixture postgres) : LancamentosSaldoTestesBase(postgres, ModoDePosicao.Ef);
