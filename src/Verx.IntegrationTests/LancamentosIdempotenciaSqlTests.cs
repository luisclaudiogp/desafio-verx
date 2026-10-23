using Lancamentos.Infrastructure.Repositories;
using Verx.IntegrationTests.Infra;

namespace Verx.IntegrationTests;

[Collection(PostgresTestes.Nome)]
public sealed class LancamentosIdempotenciaSqlTests(PostgresFixture postgres) : LancamentosIdempotenciaTestesBase(postgres, ModoDePosicao.Sql);
