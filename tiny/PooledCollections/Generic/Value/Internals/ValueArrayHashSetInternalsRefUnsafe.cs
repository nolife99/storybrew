namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct ValueArrayHashSetInternalsRefUnsafe<T>
{
    public readonly int FreeEntryIndex, Collisions;
    public readonly ulong FastModBucketsMultiplier;

    public readonly bool ClearEntries;

    public readonly ArrayEntry<T>[] Entries;
    public readonly int[] Buckets;

    public readonly ArrayPool<ArrayEntry<T>> EntryPool;
    public readonly ArrayPool<int> BucketPool;

    internal ValueArrayHashSetInternalsRefUnsafe(scoped ref readonly ValueArrayHashSet<T> source)
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
}

partial class CollectionInternals
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArrayHashSetInternalsRefUnsafe<T> GetUnsafeRef<T>(
        this scoped ref readonly ValueArrayHashSet<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<ArrayEntry<T>> AsSpan<T>(this scoped ref readonly ValueArrayHashSet<T> source)
        => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(source._entries), source._freeEntryIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<ArrayEntry<T>> AsMemory<T>(this scoped ref readonly ValueArrayHashSet<T> source)
        => new(source._entries, 0, source.Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this scoped ref readonly ValueArrayHashSet<T> source,
        out ArrayEntry<T>[] entries,
        out int count)
    {
        entries = source._entries;
        count = source.Count;
    }
}