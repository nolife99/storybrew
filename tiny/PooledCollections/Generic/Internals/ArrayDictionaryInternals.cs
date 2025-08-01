namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct ArrayDictionaryInternals<TKey, TValue> : IDisposable
{
    public readonly int FreeEntryIndex, Collisions;
    public readonly ulong FastModBucketsMultiplier;

    public readonly bool ClearEntries, ClearValues;

    public readonly ArrayEntry<TKey>[] Entries;
    public readonly TValue[] Values;
    public readonly int[] Buckets;

    public readonly ArrayPool<ArrayEntry<TKey>> EntryPool;
    public readonly ArrayPool<TValue> ValuePool;
    public readonly ArrayPool<int> BucketPool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ArrayDictionaryInternals(ArrayDictionary<TKey, TValue> source)
    {
        FreeEntryIndex = source._freeEntryIndex;
        Collisions = source._collisions;
        FastModBucketsMultiplier = source._fastModBucketsMultiplier;

        ClearEntries = ArrayDictionary<TKey, TValue>.s_clearEntries;
        ClearValues = ArrayDictionary<TKey, TValue>.s_clearValues;

        Entries = source._entries;
        Values = source._values;
        Buckets = source._buckets;

        EntryPool = source._entryPool;
        ValuePool = source._valuePool;
        BucketPool = source._bucketPool;
    }

    public void Dispose()
    {
        if (Buckets is not null) BucketPool?.Return(Buckets);

        if (Entries is not null) EntryPool?.Return(Entries, ClearEntries);

        if (Values is not null) ValuePool?.Return(Values, ClearValues);
    }
}

partial class CollectionInternals
{
    public static ArrayDictionaryInternals<TKey, TValue> TransferOwner<TKey, TValue>(
        this ArrayDictionary<TKey, TValue> source)
    {
        ArrayDictionaryInternals<TKey, TValue> internals = new(source);

        source._buckets = null;
        source._entries = null;
        source._values = null;
        source.Dispose();

        return internals;
    }
}