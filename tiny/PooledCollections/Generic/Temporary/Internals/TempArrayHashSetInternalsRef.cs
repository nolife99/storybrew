namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly ref struct TempArrayHashSetInternalsRef<T>
{
    public readonly int FreeEntryIndex, Collisions;
    public readonly ulong FastModBucketsMultiplier;

    public readonly bool ClearEntries;

    public readonly ReadOnlySpan<ArrayEntry<T>> Entries;
    public readonly ReadOnlySpan<int> Buckets;

    public readonly ArrayPool<ArrayEntry<T>> EntryPool;
    public readonly ArrayPool<int> BucketPool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TempArrayHashSetInternalsRef(scoped ref readonly TempArrayHashSet<T> source)
    {
        FreeEntryIndex = source._freeEntryIndex;
        Collisions = source._collisions;
        FastModBucketsMultiplier = source._fastModBucketsMultiplier;

        ClearEntries = TempArrayHashSet<T>.s_clearEntries;

        Entries = source._entries;
        Buckets = source._buckets;

        EntryPool = source._entryPool;
        BucketPool = source._bucketPool;
    }
}

partial class CollectionInternals
{
    public static TempArrayHashSetInternalsRef<T> GetRef<T>(this scoped ref readonly TempArrayHashSet<T> source)
        => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<ArrayEntry<T>> AsReadOnlySpan<T>(this scoped ref readonly TempArrayHashSet<T> source)
        => MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(source._entries),
            source._freeEntryIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<ArrayEntry<T>> AsReadOnlyMemory<T>(this scoped ref readonly TempArrayHashSet<T> source)
        => new(source._entries, 0, source._freeEntryIndex);
}