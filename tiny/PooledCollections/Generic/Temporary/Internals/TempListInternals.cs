namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct TempListInternals<T> : IDisposable
{
    public readonly int Size, Version;
    public readonly bool ClearItems;
    public readonly T[] Items;
    public readonly ArrayPool<T> Pool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TempListInternals(scoped ref readonly TempList<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearItems = TempList<T>.s_clearItems;
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
    public static TempListInternals<T> TransferOwner<T>(this scoped ref TempList<T> source)
    {
        TempListInternals<T> internals = new(in source);
        source.Dispose();

        source = ref Unsafe.NullRef<TempList<T>>();

        return internals;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> ToTempArray<T>(this scoped ref TempList<T> source)
    {
        var internals = TransferOwner(ref source);

        return new() { _array = internals.Items, _length = internals.Size, _pool = internals.Pool };
    }
}