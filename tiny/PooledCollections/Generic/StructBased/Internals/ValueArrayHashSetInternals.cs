#pragma warning disable CS8632

namespace Tiny.PooledCollections.Generic.StructBased.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct ValueArrayHashSetInternals<T> : IDisposable
{
    [NonSerialized] public readonly int FreeEntryIndex;
    [NonSerialized] public readonly int Collisions;
    [NonSerialized] public readonly ulong FastModBucketsMultiplier;

    [NonSerialized] public readonly bool ClearEntries;

    [NonSerialized] public readonly ArrayEntry<T>[] Entries;
    [NonSerialized] public readonly int[] Buckets;

    [NonSerialized] public readonly ArrayPool<ArrayEntry<T>> EntryPool;
    [NonSerialized] public readonly ArrayPool<int> BucketPool;

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

partial class ValueCollectionInternals
{
    /// <summary>Returns a structure that holds ownership of internal fields of <paramref name="source"/>.</summary>
    /// <remarks>Afterward <paramref name="source"/> will be disposed.</remarks>
    public static ValueArrayHashSetInternals<T> TakeOwnership<T>(this scoped ref ValueArrayHashSet<T> source)
    {
        ValueArrayHashSetInternals<T> internals = new(ref source);
        source.Dispose();

        source = Unsafe.NullRef<ValueArrayHashSet<T>>();

        return internals;
    }
}