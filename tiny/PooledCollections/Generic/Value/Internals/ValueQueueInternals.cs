namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct ValueQueueInternals<T> : IDisposable
{
    public readonly int Head, Tail, Size, Version;
    public readonly bool ClearArray;
    public readonly T[] Array;
    public readonly ArrayPool<T> Pool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueQueueInternals(scoped ref readonly ValueQueue<T> source)
    {
        Head = source._head;
        Tail = source._tail;
        Size = source._size;
        Version = source._version;
        ClearArray = ValueQueue<T>.s_clearArray;
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
    public static ValueQueueInternals<T> TransferOwner<T>(this scoped ref ValueQueue<T> source)
    {
        ValueQueueInternals<T> internals = new(ref source);
        source.Dispose();

        source = Unsafe.NullRef<ValueQueue<T>>();

        return internals;
    }
}