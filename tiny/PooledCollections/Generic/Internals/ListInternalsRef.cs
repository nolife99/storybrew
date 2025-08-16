namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly ref struct ListInternalsRef<T>
{
    public readonly int Size, Version;
    public readonly bool ClearItems;
    public readonly ReadOnlySpan<T> Items;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ListInternalsRef(PooledList<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearItems = PooledList<T>.s_clearItems;
        Items = source._items;
    }
}

partial class CollectionInternals
{
    public static ListInternalsRef<T> GetRef<T>(this PooledList<T> source) => new(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this PooledList<T> source)
        => MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(source._items), source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this PooledList<T> source)
        => new(source._items, 0, source._size);
}