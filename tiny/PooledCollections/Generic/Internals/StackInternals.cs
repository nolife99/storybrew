namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct StackInternals<T> : IDisposable
{
    public readonly int Size, Version;
    public readonly bool ClearArray;
    public readonly T[] Array;
    public readonly ArrayPool<T> Pool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal StackInternals(PooledStack<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearArray = PooledStack<T>.s_clearArray;
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
    public static StackInternals<T> TransferOwner<T>(this PooledStack<T> source)
    {
        StackInternals<T> internals = new(source);

        source._array = null;
        source.Dispose();

        return internals;
    }
}