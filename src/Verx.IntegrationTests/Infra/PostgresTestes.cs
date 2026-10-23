namespace Verx.IntegrationTests.Infra;

[CollectionDefinition(Nome)]
public sealed class PostgresTestes : ICollectionFixture<PostgresFixture>
{
    public const string Nome = "postgres";
}
