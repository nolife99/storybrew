namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct TempArrayInternals<T> : IDisposable
{
    public readonly int Length;
    public readonly bool ClearArray;
    public readonly T[] Array;
    public readonly ArrayPool<T> Pool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TempArrayInternals(scoped ref readonly TempArray<T> source)
    {
        Length = source._length;
        ClearArray = TempArray<T>.s_clearArray;
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
    public static TempArrayInternals<T> TransferOwner<T>(this scoped ref TempArray<T> source)
    {
        TempArrayInternals<T> internals = new(in source);
        source.Dispose();

        source = Unsafe.NullRef<TempArray<T>>();

        return internals;
    }
}