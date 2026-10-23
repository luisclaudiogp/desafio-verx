using System.Collections;
using MassTransit;

namespace Consolidado.UnitTests.Infrastructure;

internal sealed class FakeBatch<T>(IReadOnlyList<ConsumeContext<T>> itens) : Batch<T> where T : class
{
    public BatchCompletionMode Mode => BatchCompletionMode.Size;
    public DateTime FirstMessageReceived => DateTime.UtcNow;
    public DateTime LastMessageReceived => DateTime.UtcNow;
    public int Length => itens.Count;
    public int Count => itens.Count;

    public ConsumeContext<T> this[int index] => itens[index];

    public IEnumerator<ConsumeContext<T>> GetEnumerator() => itens.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
