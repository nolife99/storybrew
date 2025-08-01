namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly ref struct ArrayHashSetInternalsRef<T>
{
    public readonly int FreeEntryIndex, Collisions;
    public readonly ulong FastModBucketsMultiplier;

    public readonly bool ClearEntries;

    public readonly ReadOnlySpan<ArrayEntry<T>> Entries;
    public readonly ReadOnlySpan<int> Buckets;

    public readonly ArrayPool<ArrayEntry<T>> EntryPool;
    public readonly ArrayPool<int> BucketPool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ArrayHashSetInternalsRef(ArrayHashSet<T> source)
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
}

partial class CollectionInternals
{
    public static ArrayHashSetInternalsRef<T> GetRef<T>(this ArrayHashSet<T> source) => new(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<ArrayEntry<T>> AsReadOnlySpan<T>(this ArrayHashSet<T> source)
        => MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(source._entries),
            source._freeEntryIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<ArrayEntry<T>> AsReadOnlyMemory<T>(this ArrayHashSet<T> source)
        => new(source._entries, 0, source.Count);
}