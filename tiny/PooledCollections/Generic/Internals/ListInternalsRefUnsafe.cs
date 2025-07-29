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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ListInternalsRefUnsafe<T> GetRef<T>(PooledList<T> source) => new(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this PooledList<T> source)
        => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(source._items), source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this PooledList<T> source) => source._items.AsMemory(0, source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this PooledList<T> source, out T[] items, out int count)
    {
        items = source._items;
        count = source._size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> GetInsertSpan<T>(this PooledList<T> source, int index, int count)
        => source.GetInsertSpan(index, count, true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> GetInsertSpan<T>(this PooledList<T> source, int index, int count, bool clearSpan)
        => source.GetInsertSpan(index, count, clearSpan);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> GetAddSpan<T>(this PooledList<T> source, int count)
        => source.GetInsertSpan(source._size, count, true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> GetAddSpan<T>(this PooledList<T> source, int count, bool clearSpan)
        => source.GetInsertSpan(source._size, count, clearSpan);
}