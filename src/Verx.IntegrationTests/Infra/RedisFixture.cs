using Testcontainers.Redis;

namespace Verx.IntegrationTests.Infra;

public sealed class RedisFixture : IAsyncLifetime
{
    private RedisContainer? _container;

    public string ConnectionString => _container!.GetConnectionString() + ",abortConnect=false,connectTimeout=1000,syncTimeout=1000";

    public async Task InitializeAsync()
    {
        if (!DockerFactAttribute.DockerDisponivel) return;

        _container = new RedisBuilder("redis:7-alpine").Build();
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
