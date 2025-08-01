namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct ArrayHashSetInternals<T> : IDisposable
{
    public readonly int FreeEntryIndex, Collisions;
    public readonly ulong FastModBucketsMultiplier;

    public readonly bool ClearEntries;

    public readonly ArrayEntry<T>[] Entries;
    public readonly int[] Buckets;

    public readonly ArrayPool<ArrayEntry<T>> EntryPool;
    public readonly ArrayPool<int> BucketPool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ArrayHashSetInternals(ArrayHashSet<T> source)
    {
        FreeEntryIndex = source._freeEntryIndex;
        Collisions = source._collisions;
        FastModBucketsMultiplier = source._fastModBucketsMultiplier;

        ClearEntries = ArrayHashSet<T>.s_clearEntries;

        Entries = source._entries;
        Buckets = source._buckets;

        EntryPool = source._entryPool;
        BucketPool = source._bucketPool;
    }

    public void Dispose()
    {
        if (Buckets is not null) BucketPool?.Return(Buckets);

        if (Entries is not null) EntryPool?.Return(Entries, ClearEntries);
    }
}

partial class CollectionInternals
{
    public static ArrayHashSetInternals<T> TransferOwner<T>(this ArrayHashSet<T> source)
    {
        ArrayHashSetInternals<T> internals = new(source);

        source._buckets = null;
        source._entries = null;
        source.Dispose();

        return internals;
    }
}