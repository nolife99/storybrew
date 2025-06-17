namespace Tiny.PooledCollections.Generic.StructBased.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct ValueStackInternals<T> : IDisposable
{
    [NonSerialized] public readonly int Size;
    [NonSerialized] public readonly int Version;
    [NonSerialized] public readonly bool ClearArray;
    [NonSerialized] public readonly T[] Array;
    [NonSerialized] public readonly ArrayPool<T> Pool;

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

partial class ValueCollectionInternals
{
    /// <summary>Returns a structure that holds ownership of internal fields of <paramref name="source"/>.</summary>
    /// <remarks>Afterward <paramref name="source"/> will be disposed.</remarks>
    public static ValueStackInternals<T> TakeOwnership<T>(this scoped ref ValueStack<T> source)
    {
        ValueStackInternals<T> internals = new(ref source);
        source.Dispose();

        source = Unsafe.NullRef<ValueStack<T>>();

        return internals;
    }
}