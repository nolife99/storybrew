namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Buffers;
using System.Collections.Generic;

public readonly struct HashSetInternals<T> : IDisposable
{
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
    public readonly ulong FastModMultiplier;
#endif

    public readonly int Count, FreeList, FreeCount, Version;
    public readonly bool ClearEntries;

    public readonly int[] Buckets;
    public readonly Entry<T>[] Entries;
    public readonly IEqualityComparer<T> Comparer;

    public readonly ArrayPool<int> BucketPool;
    public readonly ArrayPool<Entry<T>> EntryPool;

    internal HashSetInternals(PooledHashSet<T> source)
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
        BucketPool = source._bucketPool;
        EntryPool = source._entryPool;
    }

    public void Dispose()
    {
        if (Buckets is not null) BucketPool?.Return(Buckets);

        if (Entries is not null) EntryPool?.Return(Entries, ClearEntries);
    }
}

partial class CollectionInternals
{
    public static HashSetInternals<T> TransferOwner<T>(PooledHashSet<T> source)
    {
        var internals = new HashSetInternals<T>(source);

        source._buckets = null;
        source._entries = null;
        source.Dispose();

        return internals;
    }
}