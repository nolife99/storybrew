namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Buffers;

public readonly struct StackInternals<T> : IDisposable
{
    [NonSerialized] public readonly int Size;
    [NonSerialized] public readonly int Version;
    [NonSerialized] public readonly bool ClearArray;
    [NonSerialized] public readonly T[] Array;
    [NonSerialized] public readonly ArrayPool<T> Pool;

    public StackInternals(PooledStack<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearArray = PooledStack<T>.s_clearArray;
        Array = source._array;
        Pool = source._pool;
    }

    public void Dispose()
    {
        if (!Array.IsNullOrEmpty())
            try
            {
                Pool?.Return(Array, ClearArray);
            }
            catch { }
    }
}

partial class CollectionInternals
{
    /// <summary>Returns a structure that holds ownership of internal fields of <paramref name="source"/>.</summary>
    /// <remarks>Afterward <paramref name="source"/> will be disposed.</remarks>
    public static StackInternals<T> TakeOwnership<T>(PooledStack<T> source)
    {
        var internals = new StackInternals<T>(source);

        source._array = null;
        source.Dispose();

        return internals;
    }
}