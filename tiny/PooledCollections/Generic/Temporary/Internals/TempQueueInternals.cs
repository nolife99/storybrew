namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Buffers;

public readonly struct TempQueueInternals<T> : IDisposable
{
    [NonSerialized] public readonly int Head;
    [NonSerialized] public readonly int Tail;
    [NonSerialized] public readonly int Size;
    [NonSerialized] public readonly int Version;
    [NonSerialized] public readonly bool ClearArray;
    [NonSerialized] public readonly T[] Array;
    [NonSerialized] public readonly ArrayPool<T> Pool;

    internal TempQueueInternals(scoped ref readonly TempQueue<T> source)
    {
        Head = source._head;
        Tail = source._tail;
        Size = source._size;
        Version = source._version;
        ClearArray = TempQueue<T>.s_clearArray;
        Array = source._array;
        Pool = source._pool;
    }

    public void Dispose()
    {
        if (Array is not null) Pool?.Return(Array, ClearArray);
    }
}

partial class TempCollectionInternals
{
    /// <summary>Returns a structure that holds ownership of internal fields of <paramref name="source"/>.</summary>
    /// <remarks>Afterward <paramref name="source"/> will be disposed.</remarks>
    public static TempQueueInternals<T> TransferOwner<T>(this scoped ref TempQueue<T> source)
    {
        var internals = new TempQueueInternals<T>(ref source);
        source.Dispose();

        return internals;
    }
}