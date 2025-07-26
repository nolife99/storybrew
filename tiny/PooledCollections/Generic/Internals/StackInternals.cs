namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Buffers;

public readonly struct StackInternals<T> : IDisposable
{
    public readonly int Size, Version;
    public readonly bool ClearArray;
    public readonly T[] Array;
    public readonly ArrayPool<T> Pool;

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
    /// <summary>Returns a structure that holds ownership of internal fields of <paramref name="source"/>.</summary>
    /// <remarks>Afterward <paramref name="source"/> will be disposed.</remarks>
    public static StackInternals<T> TransferOwner<T>(PooledStack<T> source)
    {
        var internals = new StackInternals<T>(source);

        source._array = null;
        source.Dispose();

        return internals;
    }
}