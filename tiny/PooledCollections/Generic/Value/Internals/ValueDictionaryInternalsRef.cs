namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly ref struct ValueDictionaryInternalsRef<TKey, TValue>
{
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
    public readonly ulong FastModMultiplier;
#endif

    public readonly int Count, FreeList, FreeCount, Version;
    public readonly bool IsReferenceKey, IsReferenceValue, ClearEntries;

    public readonly ReadOnlySpan<int> Buckets;
    public readonly ReadOnlySpan<Entry<TKey, TValue>> Entries;
    public readonly IEqualityComparer<TKey> Comparer;

    internal ValueDictionaryInternalsRef(scoped ref readonly ValueDictionary<TKey, TValue> source)
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
    }
}

partial class CollectionInternals
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueDictionaryInternalsRef<TKey, TValue> GetRef<TKey, TValue>(
        this scoped ref readonly ValueDictionary<TKey, TValue> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<Entry<TKey, TValue>> AsReadOnlySpan<TKey, TValue>(
        this scoped ref readonly ValueDictionary<TKey, TValue> source)
        => MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(source._entries), source._count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<Entry<TKey, TValue>> AsReadOnlyMemory<TKey, TValue>(
        this scoped ref readonly ValueDictionary<TKey, TValue> source) => new(source._entries, 0, source._count);
}