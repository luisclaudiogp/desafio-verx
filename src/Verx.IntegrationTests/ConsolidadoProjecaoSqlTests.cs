using Consolidado.Infrastructure.Projecao;
using Verx.IntegrationTests.Infra;

namespace Verx.IntegrationTests;

[Collection(PostgresTestes.Nome)]
public sealed class ConsolidadoProjecaoSqlTests(PostgresFixture postgres) : ConsolidadoProjecaoTestesBase(postgres, ModoDeProjecao.Sql);
