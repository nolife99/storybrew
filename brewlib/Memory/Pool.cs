namespace BrewLib.Memory;

using System;
using System.Collections.Concurrent;
using System.Threading;

public sealed class Pool<T>(Action<T> disposer = null) where T : class, new()
{
    readonly ConcurrentQueue<T> queue = new();
    T fastItem;

    public T Retrieve()
    {
        var item = fastItem;
        if (item is not null && Interlocked.CompareExchange(ref fastItem, null, item) == item ||
            queue.TryDequeue(out item)) return item;

        return new();
    }

    public void Release(T obj)
    {
        disposer?.Invoke(obj);

        if (fastItem is not null || Interlocked.CompareExchange(ref fastItem, obj, null) is not null) queue.Enqueue(obj);
    }
}