namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly ref struct ArrayDictionaryInternalsRef<TKey, TValue>
{
    public readonly int FreeEntryIndex;
    public readonly int Collisions;
    public readonly ulong FastModBucketsMultiplier;

    public readonly bool ClearEntries;
    public readonly bool ClearValues;

    public readonly ReadOnlySpan<ArrayEntry<TKey>> Entries;
    public readonly ReadOnlySpan<TValue> Values;
    public readonly ReadOnlySpan<int> Buckets;

    public readonly ArrayPool<ArrayEntry<TKey>> EntryPool;
    public readonly ArrayPool<TValue> ValuePool;
    public readonly ArrayPool<int> BucketPool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ArrayDictionaryInternalsRef(ArrayDictionary<TKey, TValue> source)
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
}

partial class CollectionInternals
{
    public static ArrayDictionaryInternalsRef<TKey, TValue> GetRef<TKey, TValue>(
        this ArrayDictionary<TKey, TValue> source)
        => new(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AsReadOnlySpan<TKey, TValue>(this ArrayDictionary<TKey, TValue> source,
        out ReadOnlySpan<ArrayEntry<TKey>> keys,
        out ReadOnlySpan<TValue> values)
    {
        keys = KeysAsReadOnlySpan(source);
        values = ValuesAsReadOnlySpan(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<ArrayEntry<TKey>>
        KeysAsReadOnlySpan<TKey, TValue>(this ArrayDictionary<TKey, TValue> source)
        => MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(source._entries),
            source._freeEntryIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<TValue> ValuesAsReadOnlySpan<TKey, TValue>(this ArrayDictionary<TKey, TValue> source)
        => MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(source._values),
            source._freeEntryIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AsReadOnlyMemory<TKey, TValue>(this ArrayDictionary<TKey, TValue> source,
        out ReadOnlyMemory<ArrayEntry<TKey>> keys,
        out ReadOnlyMemory<TValue> values)
    {
        keys = KeysAsReadOnlyMemory(source);
        values = ValuesAsReadOnlyMemory(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<ArrayEntry<TKey>> KeysAsReadOnlyMemory<TKey, TValue>(
        this ArrayDictionary<TKey, TValue> source)
        => new(source._entries, 0, source._freeEntryIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<TValue> ValuesAsReadOnlyMemory<TKey, TValue>(this ArrayDictionary<TKey, TValue> source)
        => new(source._values, 0, source._freeEntryIndex);
}