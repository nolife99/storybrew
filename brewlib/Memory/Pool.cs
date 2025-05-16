namespace BrewLib.Memory;

using System;
using System.Collections.Concurrent;
using Microsoft.IO;

public sealed class Pool<T>(Action<T> disposer = null) where T : new()
{
    readonly IProducerConsumerCollection<T> queue = new ConcurrentBag<T>();

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

public static class Pool
{
    public static readonly RecyclableMemoryStreamManager PooledMemoryStreamManager = new();
}