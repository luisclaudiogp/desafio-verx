using Consolidado.Infrastructure.Projecao;
using Verx.IntegrationTests.Infra;

namespace Verx.IntegrationTests;

[Collection(PostgresTestes.Nome)]
public sealed class ConsolidadoSnapshotsSqlTests(PostgresFixture postgres) : ConsolidadoSnapshotsTestesBase(postgres, ModoDeProjecao.Sql);
