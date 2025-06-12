namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Buffers;
using System.Collections.Generic;

public readonly struct TempHashSetInternals<T> : IDisposable
{
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
    [NonSerialized] public readonly ulong FastModMultiplier;
#endif

    [NonSerialized] public readonly int Count;
    [NonSerialized] public readonly int FreeList;
    [NonSerialized] public readonly int FreeCount;
    [NonSerialized] public readonly int Version;
    [NonSerialized] public readonly bool ClearEntries;

    [NonSerialized] public readonly int[] Buckets;
    [NonSerialized] public readonly Entry<T>[] Entries;
    [NonSerialized] public readonly IEqualityComparer<T> Comparer;

    [NonSerialized] public readonly ArrayPool<int> BucketPool;
    [NonSerialized] public readonly ArrayPool<Entry<T>> EntryPool;

    internal TempHashSetInternals(scoped ref readonly TempHashSet<T> source)
    {
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        FastModMultiplier = source._fastModMultiplier;
#endif

        Count = source._count;
        FreeList = source._freeList;
        FreeCount = source._freeCount;
        Version = source._version;
        ClearEntries = TempHashSet<T>.s_clearEntries;
        Buckets = source._buckets;
        Entries = source._entries;
        Comparer = source._comparer;
        BucketPool = source._bucketPool;
        EntryPool = source._entryPool;
    }

    public void Dispose()
    {
        if (!Buckets.IsNullOrEmpty())
            try
            {
                BucketPool?.Return(Buckets);
            }
            catch { }

        if (!Entries.IsNullOrEmpty())
            try
            {
                EntryPool?.Return(Entries, ClearEntries);
            }
            catch { }
    }
}

partial class TempCollectionInternals
{
    /// <summary>Returns a structure that holds ownership of internal fields of <paramref name="source"/>.</summary>
    /// <remarks>Afterward <paramref name="source"/> will be disposed.</remarks>
    public static TempHashSetInternals<T> TakeOwnership<T>(this scoped ref TempHashSet<T> source)
    {
        var internals = new TempHashSetInternals<T>(in source);
        source.Dispose();

        return internals;
    }
}