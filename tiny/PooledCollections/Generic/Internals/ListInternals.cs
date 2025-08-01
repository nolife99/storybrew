namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct ListInternals<T> : IDisposable
{
    public readonly int Size, Version;
    public readonly bool ClearItems;
    public readonly T[] Items;
    public readonly ArrayPool<T> Pool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    public static ListInternals<T> TransferOwner<T>(this PooledList<T> source)
    {
        ListInternals<T> internals = new(source);

        source._items = null;
        source.Dispose();

        return internals;
    }
}