using System;
using DynamicIsland.Core;
using Xunit;

namespace DynamicIsland.Core.Tests;

public class PeekEventQueueTests
{
    [Fact]
    public void EmptyQueue_TryDequeueReturnsFalse()
    {
        var queue = new PeekEventQueue();
        var result = queue.TryDequeue(out var evt);
        Assert.False(result);
        Assert.Null(evt);
    }

    [Fact]
    public void EnqueueThenDequeue_ReturnsInFifoOrder()
    {
        var queue = new PeekEventQueue();
        var first = new PeekEvent("icon-a", "First", TimeSpan.FromSeconds(2));
        var second = new PeekEvent("icon-b", "Second", TimeSpan.FromSeconds(2));

        queue.Enqueue(first);
        queue.Enqueue(second);

        queue.TryDequeue(out var dequeuedFirst);
        queue.TryDequeue(out var dequeuedSecond);

        Assert.Equal(first, dequeuedFirst);
        Assert.Equal(second, dequeuedSecond);
    }

    [Fact]
    public void Count_ReflectsQueuedItems()
    {
        var queue = new PeekEventQueue();
        queue.Enqueue(new PeekEvent("icon", "Text", TimeSpan.FromSeconds(1)));
        Assert.Equal(1, queue.Count);

        queue.TryDequeue(out _);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Clear_EmptiesQueue()
    {
        var queue = new PeekEventQueue();
        queue.Enqueue(new PeekEvent("icon", "Text", TimeSpan.FromSeconds(1)));
        queue.Clear();
        Assert.Equal(0, queue.Count);
    }
}
