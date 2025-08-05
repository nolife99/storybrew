namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct ValueListInternals<T> : IDisposable
{
    public readonly int Size, Version;
    public readonly bool ClearItems;
    public readonly T[] Items;
    public readonly ArrayPool<T> Pool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

partial class CollectionInternals
{
    public static ValueListInternals<T> TransferOwner<T>(scoped ref ValueList<T> source)
    {
        ValueListInternals<T> internals = new(source);
        source.Dispose();

        source = Unsafe.NullRef<ValueList<T>>();

        return internals;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> ToValueArray<T>(scoped ref ValueList<T> source)
    {
        var internals = TransferOwner(ref source);

        return new() { _array = internals.Items, _length = internals.Size, Pool = internals.Pool };
    }
}