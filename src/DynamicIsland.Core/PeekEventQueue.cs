using System.Collections.Generic;

namespace DynamicIsland.Core;

public sealed class PeekEventQueue
{
    private readonly Queue<PeekEvent> _queue = new();

    public int Count => _queue.Count;

    public void Enqueue(PeekEvent peekEvent) => _queue.Enqueue(peekEvent);

    public bool TryDequeue(out PeekEvent? peekEvent) => _queue.TryDequeue(out peekEvent);

    public void Clear() => _queue.Clear();
}
