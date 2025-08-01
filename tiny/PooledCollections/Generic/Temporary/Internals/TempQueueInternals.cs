namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct TempQueueInternals<T> : IDisposable
{
    public readonly int Head, Tail, Size, Version;
    public readonly bool ClearArray;
    public readonly T[] Array;
    public readonly ArrayPool<T> Pool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

partial class CollectionInternals
{
    public static TempQueueInternals<T> TransferOwner<T>(this scoped ref TempQueue<T> source)
    {
        TempQueueInternals<T> internals = new(ref source);
        source.Dispose();

        source = Unsafe.NullRef<TempQueue<T>>();

        return internals;
    }
}