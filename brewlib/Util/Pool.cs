namespace BrewLib.Util;

using System;
using System.Collections.Concurrent;

public sealed class Pool<T>(Action<T> disposer = null) where T : new()
{
    readonly ConcurrentQueue<T> queue = new();
    public T Retrieve()
    {
        if (queue.IsEmpty) return new();
        return queue.TryDequeue(out var obj) ? obj : new();
    }

    public void Release(T obj)
    {
        disposer?.Invoke(obj);
        queue.Enqueue(obj);
    }
}