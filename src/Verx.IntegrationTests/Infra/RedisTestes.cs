namespace Verx.IntegrationTests.Infra;

[CollectionDefinition(Nome)]
public sealed class RedisTestes : ICollectionFixture<RedisFixture>
{
    public const string Nome = "redis";
}
