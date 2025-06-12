namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Buffers;

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
        if (!Array.IsNullOrEmpty())
            try
            {
                Pool?.Return(Array, ClearArray);
            }
            catch { }
    }
}

partial class TempCollectionInternals
{
    /// <summary>Returns a structure that holds ownership of internal fields of <paramref name="source"/>.</summary>
    /// <remarks>Afterward <paramref name="source"/> will be disposed.</remarks>
    public static TempArrayInternals<T> TakeOwnership<T>(scoped ref TempArray<T> source)
    {
        var internals = new TempArrayInternals<T>(in source);
        source.Dispose();

        return internals;
    }
}