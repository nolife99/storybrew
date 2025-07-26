namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Buffers;
using System.Collections.Generic;

public readonly struct DictionaryInternals<TKey, TValue> : IDisposable
{
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
    public readonly ulong FastModMultiplier;
#endif

    public readonly int Count, FreeList, FreeCount, Version;
    public readonly bool IsReferenceKey, IsReferenceValue, ClearEntries;

    public readonly int[] Buckets;
    public readonly Entry<TKey, TValue>[] Entries;
    public readonly IEqualityComparer<TKey> Comparer;

    public readonly ArrayPool<int> BucketPool;
    public readonly ArrayPool<Entry<TKey, TValue>> EntryPool;

    internal DictionaryInternals(PooledDictionary<TKey, TValue> source)
    {
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        FastModMultiplier = source._fastModMultiplier;
#endif

        Count = source._count;
        FreeList = source._freeList;
        FreeCount = source._freeCount;
        Version = source._version;
        IsReferenceKey = PooledDictionary<TKey, TValue>.s_isReferenceKey;
        IsReferenceValue = PooledDictionary<TKey, TValue>.s_isReferenceValue;
        ClearEntries = PooledDictionary<TKey, TValue>.s_clearEntries;
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
    public static DictionaryInternals<TKey, TValue> TransferOwner<TKey, TValue>(PooledDictionary<TKey, TValue> source)
    {
        var internals = new DictionaryInternals<TKey, TValue>(source);

        source._buckets = null;
        source._entries = null;
        source.Dispose();

        return internals;
    }
}