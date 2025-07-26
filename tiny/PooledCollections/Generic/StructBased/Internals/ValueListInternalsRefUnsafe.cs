namespace Tiny.PooledCollections.Generic.StructBased.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct ValueListInternalsRefUnsafe<T>
{
    public readonly int Size, Version;
    public readonly bool ClearItems;
    public readonly T[] Items;

    internal ValueListInternalsRefUnsafe(scoped ref readonly ValueList<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearItems = ValueList<T>.s_clearItems;
        Items = source._items;
    }
}

partial class ValueCollectionInternalsUnsafe
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueListInternalsRefUnsafe<T> GetUnsafeRef<T>(this scoped ref readonly ValueList<T> source)
        => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly ValueList<T> source)
        => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(source._items), source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly ValueList<T> source) => new(source._items, 0, source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this scoped ref readonly ValueList<T> source, out T[] items, out int count)
    {
        items = source._items;
        count = source._size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> GetInsertSpan<T>(this scoped ref ValueList<T> source, int index, int count)
        => source.GetInsertSpan(index, count, true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> GetInsertSpan<T>(this scoped ref ValueList<T> source, int index, int count, bool clearSpan)
        => source.GetInsertSpan(index, count, clearSpan);
}