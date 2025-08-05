namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct ValueArrayInternals<T> : IDisposable
{
    public readonly int Length;
    public readonly bool ClearArray;
    public readonly T[] Array;
    public readonly ArrayPool<T> Pool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueArrayInternals(scoped ref readonly ValueArray<T> source)
    {
        Length = source._length;
        ClearArray = ValueArray<T>.s_clearArray;
        Array = source._array;
        Pool = source.Pool;
    }

    public void Dispose()
    {
        if (Array is not null) Pool?.Return(Array, ClearArray);
    }
}

partial class CollectionInternals
{
    public static ValueArrayInternals<T> TransferOwner<T>(this scoped ref ValueArray<T> source)
    {
        ValueArrayInternals<T> internals = new(ref source);
        source.Dispose();

        source = Unsafe.NullRef<ValueArray<T>>();

        return internals;
    }
}