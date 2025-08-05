namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct ValueArrayHashSetInternals<T> : IDisposable
{
    public readonly int FreeEntryIndex, Collisions;
    public readonly ulong FastModBucketsMultiplier;

    public readonly bool ClearEntries;

    public readonly ArrayEntry<T>[] Entries;
    public readonly int[] Buckets;

    public readonly ArrayPool<ArrayEntry<T>> EntryPool;
    public readonly ArrayPool<int> BucketPool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueArrayHashSetInternals(scoped ref readonly ValueArrayHashSet<T> source)
    {
        FreeEntryIndex = source._freeEntryIndex;
        Collisions = source._collisions;
        FastModBucketsMultiplier = source._fastModBucketsMultiplier;

        ClearEntries = ValueArrayHashSet<T>.s_clearEntries;

        Entries = source._entries;
        Buckets = source._buckets;

        EntryPool = source._entryPool;
        BucketPool = source._bucketPool;
    }

    public void Dispose()
    {
        if (Buckets is not null) BucketPool.Return(Buckets);

        if (Entries is not null) EntryPool.Return(Entries, ClearEntries);
    }
}

partial class CollectionInternals
{
    public static ValueArrayHashSetInternals<T> TransferOwner<T>(this scoped ref ValueArrayHashSet<T> source)
    {
        ValueArrayHashSetInternals<T> internals = new(ref source);
        source.Dispose();

        source = Unsafe.NullRef<ValueArrayHashSet<T>>();

        return internals;
    }
}