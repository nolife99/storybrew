namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly ref struct ListInternalsRef<T>
{
    public readonly int Size, Version;
    public readonly bool ClearItems;
    public readonly ReadOnlySpan<T> Items;

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
    /// <summary>Returns a structure that holds references to internal fields of <paramref name="source"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ListInternalsRef<T> GetRef<T>(PooledList<T> source) => new(source);

    /// <summary>Returns the internal array as a <see cref="ReadOnlySpan{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this PooledList<T> source)
        => MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(source._items), source._size);

    /// <summary>Returns the internal array as a <see cref="ReadOnlyMemory{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this PooledList<T> source)
        => source._items.AsMemory(0, source._size);
}