namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct TempArrayDictionaryInternalsRefUnsafe<TKey, TValue>
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
    internal TempArrayDictionaryInternalsRefUnsafe(scoped ref readonly TempArrayDictionary<TKey, TValue> source)
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
}

partial class CollectionInternals
{
    public static TempArrayDictionaryInternalsRefUnsafe<TKey, TValue> GetUnsafeRef<TKey, TValue>(
        this scoped ref readonly TempArrayDictionary<TKey, TValue> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AsSpan<TKey, TValue>(this scoped ref readonly TempArrayDictionary<TKey, TValue> source,
        out Span<ArrayEntry<TKey>> keys,
        out Span<TValue> values)
    {
        keys = KeysAsSpan(in source);
        values = ValuesAsSpan(in source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<ArrayEntry<TKey>> KeysAsSpan<TKey, TValue>(
        this scoped ref readonly TempArrayDictionary<TKey, TValue> source)
        => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(source._entries), source._freeEntryIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<TValue> ValuesAsSpan<TKey, TValue>(this scoped ref readonly TempArrayDictionary<TKey, TValue> source)
        => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(source._values), source._freeEntryIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AsMemory<TKey, TValue>(this scoped ref readonly TempArrayDictionary<TKey, TValue> source,
        out Memory<ArrayEntry<TKey>> keys,
        out Memory<TValue> values)
    {
        keys = KeysAsMemory(in source);
        values = ValuesAsMemory(in source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<ArrayEntry<TKey>> KeysAsMemory<TKey, TValue>(
        this scoped ref readonly TempArrayDictionary<TKey, TValue> source) => new(source._entries, 0, source.Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<TValue> ValuesAsMemory<TKey, TValue>(
        this scoped ref readonly TempArrayDictionary<TKey, TValue> source) => new(source._values, 0, source.Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<TKey, TValue>(this scoped ref readonly TempArrayDictionary<TKey, TValue> source,
        out ArrayEntry<TKey>[] keys,
        out TValue[] values,
        out int count)
    {
        keys = source._entries;
        values = source._values;
        count = source.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafeKeys<TKey, TValue>(this scoped ref readonly TempArrayDictionary<TKey, TValue> source,
        out ArrayEntry<TKey>[] keys,
        out int count)
    {
        keys = source._entries;
        count = source.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafeValues<TKey, TValue>(this scoped ref readonly TempArrayDictionary<TKey, TValue> source,
        out TValue[] values,
        out int count)
    {
        values = source._values;
        count = source.Count;
    }
}