namespace Consolidado.UnitTests;

internal sealed class FixedTimeProvider(DateTimeOffset agora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => agora;
}
