namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly ref struct TempListInternalsRef<T>
{
    public readonly int Size, Version;
    public readonly bool ClearItems;
    public readonly ReadOnlySpan<T> Items;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TempListInternalsRef(scoped ref readonly TempList<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearItems = TempList<T>.s_clearItems;
        Items = source._items;
    }
}

partial class CollectionInternals
{
    public static TempListInternalsRef<T> GetRef<T>(this scoped ref readonly TempList<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly TempList<T> source)
        => MemoryMarshal.CreateReadOnlySpan(ref source._ref, source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly TempList<T> source)
        => new(source._items, 0, source._size);
}