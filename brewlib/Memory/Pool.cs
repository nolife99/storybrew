namespace BrewLib.Memory;

using System;
using System.Collections.Concurrent;

public sealed class Pool<T>(Action<T> disposer = null) where T : new()
{
    readonly IProducerConsumerCollection<T> queue = new ConcurrentQueue<T>();

    public T Retrieve()
    {
        if (queue.Count == 0) return new();

        return queue.TryTake(out var obj) ? obj : new();
    }

    public void Release(T obj)
    {
        disposer?.Invoke(obj);
        queue.TryAdd(obj);
    }
}