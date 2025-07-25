namespace Tiny.PooledCollections.Generic.StructBased.Internals;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public readonly struct ValueDictionaryInternals<TKey, TValue> : IDisposable
{
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
    [NonSerialized] public readonly ulong FastModMultiplier;
#endif

    [NonSerialized] public readonly int Count;
    [NonSerialized] public readonly int FreeList;
    [NonSerialized] public readonly int FreeCount;
    [NonSerialized] public readonly int Version;
    [NonSerialized] public readonly bool IsReferenceKey;
    [NonSerialized] public readonly bool IsReferenceValue;
    [NonSerialized] public readonly bool ClearEntries;

    [NonSerialized] public readonly int[] Buckets;
    [NonSerialized] public readonly Entry<TKey, TValue>[] Entries;
    [NonSerialized] public readonly IEqualityComparer<TKey> Comparer;

    [NonSerialized] public readonly ArrayPool<int> BucketPool;
    [NonSerialized] public readonly ArrayPool<Entry<TKey, TValue>> EntryPool;

    internal ValueDictionaryInternals(scoped ref readonly ValueDictionary<TKey, TValue> source)
    {
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        FastModMultiplier = source._fastModMultiplier;
#endif

        Count = source._count;
        FreeList = source._freeList;
        FreeCount = source._freeCount;
        Version = source._version;
        IsReferenceKey = ValueDictionary<TKey, TValue>.s_isReferenceKey;
        IsReferenceValue = ValueDictionary<TKey, TValue>.s_isReferenceValue;
        ClearEntries = ValueDictionary<TKey, TValue>.s_clearEntries;
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

partial class ValueCollectionInternals
{
    /// <summary>Returns a structure that holds ownership of internal fields of <paramref name="source"/>.</summary>
    /// <remarks>Afterward <paramref name="source"/> will be disposed.</remarks>
    public static ValueDictionaryInternals<TKey, TValue> TakeOwnership<TKey, TValue>(
        this scoped ref ValueDictionary<TKey, TValue> source)
    {
        ValueDictionaryInternals<TKey, TValue> internals = new(ref source);
        source.Dispose();

        source = Unsafe.NullRef<ValueDictionary<TKey, TValue>>();

        return internals;
    }
}