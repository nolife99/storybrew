namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct TempArrayDictionaryInternals<TKey, TValue> : IDisposable
{
    public int FreeEntryIndex { get; }
    public int Collisions { get; }
    public ulong FastModBucketsMultiplier { get; }

    public bool ClearEntries { get; }
    public bool ClearValues { get; }

    public ArrayEntry<TKey>[] Entries { get; }
    public TValue[] Values { get; }
    public int[] Buckets { get; }

    public ArrayPool<ArrayEntry<TKey>> EntryPool { get; }
    public ArrayPool<TValue> ValuePool { get; }
    public ArrayPool<int> BucketPool { get; }

    internal TempArrayDictionaryInternals(scoped ref readonly TempArrayDictionary<TKey, TValue> source)
    {
        FreeEntryIndex = source._freeEntryIndex;
        Collisions = source._collisions;
        FastModBucketsMultiplier = source._fastModBucketsMultiplier;

        ClearEntries = TempArrayDictionary<TKey, TValue>.s_clearEntries;
        ClearValues = TempArrayDictionary<TKey, TValue>.s_clearValues;

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

partial class TempCollectionInternals
{
    /// <summary>Returns a structure that holds ownership of internal fields of <paramref name="source"/>.</summary>
    /// <remarks>Afterward <paramref name="source"/> will be disposed.</remarks>
    public static TempArrayDictionaryInternals<TKey, TValue> TransferOwner<TKey, TValue>(
        this scoped ref TempArrayDictionary<TKey, TValue> source)
    {
        TempArrayDictionaryInternals<TKey, TValue> internals = new(ref source);
        source.Dispose();

        source = Unsafe.NullRef<TempArrayDictionary<TKey, TValue>>();

        return internals;
    }
}