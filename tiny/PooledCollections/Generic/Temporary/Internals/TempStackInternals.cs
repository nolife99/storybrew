namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct TempStackInternals<T> : IDisposable
{
    public readonly int Size, Version;
    public readonly bool ClearArray;
    public readonly T[] Array;
    public readonly ArrayPool<T> Pool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TempStackInternals(scoped ref readonly TempStack<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearArray = TempStack<T>.s_clearArray;
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
    public static TempStackInternals<T> TransferOwner<T>(this scoped ref TempStack<T> source)
    {
        TempStackInternals<T> internals = new(in source);
        source.Dispose();

        source = Unsafe.NullRef<TempStack<T>>();

        return internals;
    }
}