using Lancamentos.Infrastructure.Repositories;
using Verx.IntegrationTests.Infra;

namespace Verx.IntegrationTests;

[Collection(PostgresTestes.Nome)]
public sealed class LancamentosSaldoSqlTests(PostgresFixture postgres) : LancamentosSaldoTestesBase(postgres, ModoDePosicao.Sql);
