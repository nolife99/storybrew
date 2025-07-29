namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly ref struct ArrayHashSetInternalsRef<T>
{
    public readonly int FreeEntryIndex, Collisions;
    public readonly ulong FastModBucketsMultiplier;

    public readonly bool ClearEntries;

    public readonly ReadOnlySpan<ArrayEntry<T>> Entries;
    public readonly ReadOnlySpan<int> Buckets;

    public readonly ArrayPool<ArrayEntry<T>> EntryPool;
    public readonly ArrayPool<int> BucketPool;

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
    /// <summary> Returns a structure that holds references to internal fields of <paramref name="source"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayHashSetInternalsRef<T> GetRef<T>(ArrayHashSet<T> source) => new(source);

    /// <summary> Returns the internal Keys and Values arrays as a <see cref="ReadOnlySpan{T}"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<ArrayEntry<T>> AsReadOnlySpan<T>(this ArrayHashSet<T> source)
        => source._entries.AsSpan(0, source.Count);

    /// <summary> Returns the internal Keys and Values arrays as a <see cref="ReadOnlyMemory{T}"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<ArrayEntry<T>> AsReadOnlyMemory<T>(this ArrayHashSet<T> source)
        => source._entries.AsMemory(0, source.Count);
}