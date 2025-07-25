namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct ListInternalsRefUnsafe<T>
{
    public readonly int Size, Version;
    public readonly bool ClearItems;
    public readonly T[] Items;

    internal ListInternalsRefUnsafe(PooledList<T> source)
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
    public static Span<T> AsSpan<T>(this PooledList<T> source)
        => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(source._items), source._size);

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