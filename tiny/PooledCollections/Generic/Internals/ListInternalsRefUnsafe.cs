namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Runtime.CompilerServices;

public readonly struct ListInternalsRefUnsafe<T>
{
    [NonSerialized] public readonly int Size;
    [NonSerialized] public readonly int Version;
    [NonSerialized] public readonly bool ClearItems;
    [NonSerialized] public readonly T[] Items;

    public ListInternalsRefUnsafe(PooledList<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearItems = PooledList<T>.s_clearItems;
        Items = source._items;
    }
}

partial class CollectionInternalsUnsafe
{
    /// <summary>Returns a structure that holds references to internal fields of <paramref name="source"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ListInternalsRefUnsafe<T> GetRef<T>(PooledList<T> source) => new(source);

    /// <summary>Returns the internal array as a <see cref="Span{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this PooledList<T> source) => source._items.AsSpan(0, source._size);

    /// <summary>Returns the internal array as a <see cref="Memory{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this PooledList<T> source) => source._items.AsMemory(0, source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this PooledList<T> source, out T[] items, out int count)
    {
        items = source._items;
        count = source._size;
    }

    /// <summary>
    ///     Advances the <see cref="Count"/> by the number of items specified, increasing the capacity if required, then
    ///     returns a <see cref="Span{T}"/> representing the set of items to be added, allowing direct writes to that section of the
    ///     collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> GetInsertSpan<T>(PooledList<T> source, int index, int count)
        => source.GetInsertSpan(index, count, true);

    /// <summary>
    ///     Advances the <see cref="Count"/> by the number of items specified, increasing the capacity if required, then
    ///     returns a <see cref="Span{T}"/> representing the set of items to be added, allowing direct writes to that section of the
    ///     collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> GetInsertSpan<T>(PooledList<T> source, int index, int count, bool clearSpan)
        => source.GetInsertSpan(index, count, clearSpan);
}