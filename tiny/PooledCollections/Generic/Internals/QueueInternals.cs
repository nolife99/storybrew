namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Buffers;

public readonly struct QueueInternals<T> : IDisposable
{
    public readonly int Head, Tail, Size, Version;
    public readonly bool ClearArray;
    public readonly T[] Array;
    public readonly ArrayPool<T> Pool;

    internal QueueInternals(PooledQueue<T> source)
    {
        Head = source._head;
        Tail = source._tail;
        Size = source._size;
        Version = source._version;
        ClearArray = PooledQueue<T>.s_clearArray;
        Array = source._array;
        Pool = source._pool;
    }

    public void Dispose()
    {
        if (Array is not null) Pool?.Return(Array, ClearArray);
    }
}

partial class CollectionInternals
{
    /// <summary> Returns a structure that holds ownership of internal fields of <paramref name="source"/>. </summary>
    /// <remarks> Afterward <paramref name="source"/> will be disposed. </remarks>
    public static QueueInternals<T> TransferOwner<T>(PooledQueue<T> source)
    {
        var internals = new QueueInternals<T>(source);

        source._array = null;
        source.Dispose();

        return internals;
    }
}