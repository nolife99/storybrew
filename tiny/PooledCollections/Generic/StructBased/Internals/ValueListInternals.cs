namespace Tiny.PooledCollections.Generic.StructBased.Internals;

using System;
using System.Buffers;

public readonly struct ValueListInternals<T> : IDisposable
{
    public readonly int Size;
    public readonly int Version;
    public readonly bool ClearItems;
    public readonly T[] Items;
    public readonly ArrayPool<T> Pool;

    internal ValueListInternals(ValueList<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearItems = ValueList<T>.s_clearItems;
        Items = source._items;
        Pool = source._pool;
    }

    public void Dispose()
    {
        if (Items is not null) Pool?.Return(Items, ClearItems);
    }
}

partial class ValueCollectionInternals
{
    public static ValueListInternals<T> TakeOwnership<T>(scoped ref ValueList<T> source)
    {
        var internals = new ValueListInternals<T>(source);

        source._items = null;
        source.Dispose();

        return internals;
    }

    public static ValueArray<T> ToValueArray<T>(scoped ref ValueList<T> source)
    {
        var internals = TakeOwnership(ref source);

        return new() { _array = internals.Items, _length = internals.Size, Pool = internals.Pool };
    }
}