namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Buffers;

public readonly struct ListInternals<T> : IDisposable
{
    public readonly int Size, Version;
    public readonly bool ClearItems;
    public readonly T[] Items;
    public readonly ArrayPool<T> Pool;

    internal ListInternals(PooledList<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearItems = PooledList<T>.s_clearItems;
        Items = source._items;
        Pool = source._pool;
    }

    public void Dispose()
    {
        if (Items is not null) Pool?.Return(Items, ClearItems);
    }
}

partial class CollectionInternals
{
    /// <summary>Returns a structure that holds ownership of internal fields of <paramref name="source"/>.</summary>
    /// <remarks>Afterward <paramref name="source"/> will be disposed.</remarks>
    public static ListInternals<T> TransferOwner<T>(PooledList<T> source)
    {
        var internals = new ListInternals<T>(source);

        source._items = null;
        source.Dispose();

        return internals;
    }
}