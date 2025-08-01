namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly ref struct ValueListInternalsRef<T>
{
    public readonly int Size, Version;
    public readonly bool ClearItems;
    public readonly ReadOnlySpan<T> Items;

    internal ValueListInternalsRef(scoped ref readonly ValueList<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearItems = ValueList<T>.s_clearItems;
        Items = source._items;
    }
}

partial class CollectionInternals
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueListInternalsRef<T> GetRef<T>(this scoped ref readonly ValueList<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ValueList<T> source)
        => MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(source._items), source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueList<T> source)
        => new(source._items, 0, source._size);
}