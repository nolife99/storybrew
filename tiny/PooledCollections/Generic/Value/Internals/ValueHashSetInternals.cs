namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public readonly struct ValueHashSetInternals<T> : IDisposable
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueHashSetInternals(scoped ref readonly ValueHashSet<T> source)
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
    public static ValueHashSetInternals<T> TransferOwner<T>(this scoped ref ValueHashSet<T> source)
    {
        ValueHashSetInternals<T> internals = new(in source);
        source.Dispose();

        source = ref Unsafe.NullRef<ValueHashSet<T>>();

        return internals;
    }
}