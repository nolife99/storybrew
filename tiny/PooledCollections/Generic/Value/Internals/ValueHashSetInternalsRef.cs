namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly ref struct ValueHashSetInternalsRef<T>
{
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
    public readonly ulong FastModMultiplier;
#endif

    public readonly int Count, FreeList, FreeCount, Version;
    public readonly bool ClearEntries;

    public readonly ReadOnlySpan<int> Buckets;
    public readonly ReadOnlySpan<Entry<T>> Entries;
    public readonly IEqualityComparer<T> Comparer;

    internal ValueHashSetInternalsRef(scoped ref readonly ValueHashSet<T> source)
    {
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        FastModMultiplier = source._fastModMultiplier;
#endif

        Count = source._count;
        FreeList = source._freeList;
        FreeCount = source._freeCount;
        Version = source._version;
        ClearEntries = ValueHashSet<T>.s_clearEntries;
        Buckets = source._buckets;
        Entries = source._entries;
        Comparer = source._comparer;
    }
}

partial class CollectionInternals
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueHashSetInternalsRef<T> GetRef<T>(this scoped ref readonly ValueHashSet<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<Entry<T>> AsReadOnlySpan<T>(this scoped ref readonly ValueHashSet<T> source)
        => MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(source._entries), source._count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<Entry<T>> AsReadOnlyMemory<T>(this scoped ref readonly ValueHashSet<T> source)
        => new(source._entries, 0, source._count);
}