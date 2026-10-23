using Consolidado.Infrastructure.Projecao;
using Verx.IntegrationTests.Infra;

namespace Verx.IntegrationTests;

[Collection(PostgresTestes.Nome)]
public sealed class ConsolidadoSnapshotsEfTests(PostgresFixture postgres) : ConsolidadoSnapshotsTestesBase(postgres, ModoDeProjecao.Ef);
