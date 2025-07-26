namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Buffers;

public readonly struct TempListInternals<T> : IDisposable
{
    [NonSerialized] public readonly int Size;
    [NonSerialized] public readonly int Version;
    [NonSerialized] public readonly bool ClearItems;
    [NonSerialized] public readonly T[] Items;
    [NonSerialized] public readonly ArrayPool<T> Pool;

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

partial class TempCollectionInternals
{
    /// <summary>Returns a structure that holds ownership of internal fields of <paramref name="source"/>.</summary>
    /// <remarks>Afterward <paramref name="source"/> will be disposed.</remarks>
    public static TempListInternals<T> TransferOwner<T>(this scoped ref TempList<T> source)
    {
        var internals = new TempListInternals<T>(in source);
        source.Dispose();

        return internals;
    }

    public static TempArray<T> ToTempArray<T>(this scoped ref TempList<T> source)
    {
        var internals = TransferOwner(ref source);

        return new TempArray<T> { _array = internals.Items, _length = internals.Size, _pool = internals.Pool };
    }
}