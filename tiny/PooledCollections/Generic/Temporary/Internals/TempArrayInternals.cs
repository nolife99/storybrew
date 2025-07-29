namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct TempArrayInternals<T> : IDisposable
{
    [NonSerialized] public readonly int Length;
    [NonSerialized] public readonly bool ClearArray;
    [NonSerialized] public readonly T[] Array;
    [NonSerialized] public readonly ArrayPool<T> Pool;

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

partial class TempCollectionInternals
{
    /// <summary> Returns a structure that holds ownership of internal fields of <paramref name="source"/>. </summary>
    /// <remarks> Afterward <paramref name="source"/> will be disposed. </remarks>
    public static TempArrayInternals<T> TransferOwner<T>(scoped ref TempArray<T> source)
    {
        TempArrayInternals<T> internals = new(in source);
        source.Dispose();

        source = Unsafe.NullRef<TempArray<T>>();

        return internals;
    }
}