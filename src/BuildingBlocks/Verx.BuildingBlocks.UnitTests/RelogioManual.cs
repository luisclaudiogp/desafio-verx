namespace Verx.BuildingBlocks.UnitTests;

internal sealed class RelogioManual : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Read(ref _ticks);

    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero) + TimeSpan.FromTicks(Interlocked.Read(ref _ticks));

    public void Avancar(TimeSpan tempo) => Interlocked.Add(ref _ticks, tempo.Ticks);
}
