namespace Tiny.PooledCollections.Generic.StructBased.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct ValueArrayInternals<T> : IDisposable
{
    [NonSerialized] public readonly int Length;
    [NonSerialized] public readonly bool ClearArray;
    [NonSerialized] public readonly T[] Array;
    [NonSerialized] public readonly ArrayPool<T> Pool;

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

partial class ValueCollectionInternals
{
    /// <summary>Returns a structure that holds ownership of internal fields of <paramref name="source"/>.</summary>
    /// <remarks>Afterward <paramref name="source"/> will be disposed.</remarks>
    public static ValueArrayInternals<T> TakeOwnership<T>(this scoped ref ValueArray<T> source)
    {
        ValueArrayInternals<T> internals = new(ref source);
        source.Dispose();

        source = Unsafe.NullRef<ValueArray<T>>();

        return internals;
    }
}