namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public readonly struct ArrayHashSetInternalsRefUnsafe<T>
{
    public readonly int FreeEntryIndex;
    public readonly int Collisions;
    public readonly ulong FastModBucketsMultiplier;

    public readonly bool ClearEntries;

    public readonly ArrayEntry<T>[] Entries;
    public readonly int[] Buckets;

    public readonly ArrayPool<ArrayEntry<T>> EntryPool;
    public readonly ArrayPool<int> BucketPool;

    internal ArrayHashSetInternalsRefUnsafe(ArrayHashSet<T> source)
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

partial class CollectionInternalsUnsafe
{
    /// <summary> Returns a structure that holds references to internal fields of <paramref name="source"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayHashSetInternalsRefUnsafe<T> GetRef<T>(ArrayHashSet<T> source) => new(source);

    /// <summary> Returns the internal Keys and Values arrays as a <see cref="Span{T}"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<ArrayEntry<T>> AsSpan<T>(this ArrayHashSet<T> source) => source._entries.AsSpan(0, source.Count);

    /// <summary> Returns the internal Keys and Values arrays as a <see cref="Memory{T}"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<ArrayEntry<T>> AsMemory<T>(this ArrayHashSet<T> source)
        => source._entries.AsMemory(0, source.Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this ArrayHashSet<T> source, out ArrayEntry<T>[] entries, out int count)
    {
        entries = source._entries;
        count = source.Count;
    }
}