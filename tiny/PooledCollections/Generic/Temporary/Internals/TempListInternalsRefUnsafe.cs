namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct TempListInternalsRefUnsafe<T>
{
    public readonly int Size, Version;
    public readonly bool ClearItems;
    public readonly T[] Items;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TempListInternalsRefUnsafe(scoped ref readonly TempList<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearItems = TempList<T>.s_clearItems;
        Items = source._items;
    }
}

partial class CollectionInternals
{
    public static TempListInternalsRefUnsafe<T> GetUnsafeRef<T>(this scoped ref readonly TempList<T> source)
        => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly TempList<T> source)
        => MemoryMarshal.CreateSpan(ref source._ref, source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly TempList<T> source)
        => new(source._items, 0, source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this scoped ref readonly TempList<T> source, out T[] items, out int count)
    {
        items = source._items;
        count = source._size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> GetInsertSpan<T>(this scoped ref TempList<T> source, int index, int count)
        => source.GetInsertSpan(index, count, true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> GetInsertSpan<T>(this scoped ref TempList<T> source, int index, int count, bool clearSpan)
        => source.GetInsertSpan(index, count, clearSpan);
}