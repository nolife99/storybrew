namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public readonly ref struct HashSetInternalsRef<T>
{
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
    public readonly ulong FastModMultiplier;
#endif

    public readonly int Count, FreeList, FreeCount, Version;
    public readonly bool ClearEntries;

    public readonly ReadOnlySpan<int> Buckets;
    public readonly ReadOnlySpan<Entry<T>> Entries;
    public readonly IEqualityComparer<T> Comparer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal HashSetInternalsRef(PooledHashSet<T> source)
    {
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        FastModMultiplier = source._fastModMultiplier;
#endif

        Count = source._count;
        FreeList = source._freeList;
        FreeCount = source._freeCount;
        Version = source._version;
        ClearEntries = PooledHashSet<T>.s_clearEntries;
        Buckets = source._buckets;
        Entries = source._entries;
        Comparer = source._comparer;
    }
}

partial class CollectionInternals
{
    public static HashSetInternalsRef<T> GetRef<T>(this PooledHashSet<T> source) => new(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<Entry<T>> AsReadOnlySpan<T>(this PooledHashSet<T> source)
        => source._entries.AsSpan(0, source._count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<Entry<T>> AsReadOnlyMemory<T>(this PooledHashSet<T> source)
        => source._entries.AsMemory(0, source._count);
}