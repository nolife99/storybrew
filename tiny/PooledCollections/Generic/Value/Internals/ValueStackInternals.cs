namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct ValueStackInternals<T> : IDisposable
{
    public readonly int Size, Version;
    public readonly bool ClearArray;
    public readonly T[] Array;
    public readonly ArrayPool<T> Pool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueStackInternals(scoped ref readonly ValueStack<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearArray = ValueStack<T>.s_clearArray;
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
    public static ValueStackInternals<T> TransferOwner<T>(this scoped ref ValueStack<T> source)
    {
        ValueStackInternals<T> internals = new(ref source);
        source.Dispose();

        source = Unsafe.NullRef<ValueStack<T>>();

        return internals;
    }
}