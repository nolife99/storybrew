namespace Tiny.PooledCollections.Generic.StructBased.Internals;

using System;
using System.Buffers;

public readonly struct ValueListInternals<T> : IDisposable
{
    [NonSerialized] public readonly int Size;
    [NonSerialized] public readonly int Version;
    [NonSerialized] public readonly bool ClearItems;
    [NonSerialized] public readonly T[] Items;
    [NonSerialized] public readonly ArrayPool<T> Pool;

    internal ValueListInternals(in ValueList<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearItems = ValueList<T>.s_clearItems;
        Items = source._items;
        Pool = source._pool;
    }

    public void Dispose()
    {
        if (Items is not null && Items.Length > 0)
            try
            {
                Pool?.Return(Items, ClearItems);
            }
            catch { }
    }
}

partial class ValueCollectionInternals
{
    /// <summary>Returns a structure that holds ownership of internal fields of <paramref name="source"/>.</summary>
    /// <remarks>Afterward <paramref name="source"/> will be disposed.</remarks>
    public static ValueListInternals<T> TakeOwnership<T>(ref ValueList<T> source)
    {
        var internals = new ValueListInternals<T>(source);

        source._items = null;
        source.Dispose();

        return internals;
    }

    public static ValueArray<T> ToValueArray<T>(ref ValueList<T> source)
    {
        var internals = TakeOwnership(ref source);

        return new ValueArray<T> { _array = internals.Items, _length = internals.Size, _pool = internals.Pool };
    }
}