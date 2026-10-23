using Consolidado.Infrastructure.Projecao;
using Verx.IntegrationTests.Infra;

namespace Verx.IntegrationTests;

[Collection(PostgresTestes.Nome)]
public sealed class ConsolidadoProjecaoEfTests(PostgresFixture postgres) : ConsolidadoProjecaoTestesBase(postgres, ModoDeProjecao.Ef);
