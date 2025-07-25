namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public readonly ref struct DictionaryInternalsRef<TKey, TValue>
{
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
    public readonly ulong FastModMultiplier;
#endif

    public readonly int Count;
    public readonly int FreeList;
    public readonly int FreeCount;
    public readonly int Version;
    public readonly bool IsReferenceKey;
    public readonly bool IsReferenceValue;
    public readonly bool ClearEntries;

    public readonly ReadOnlySpan<int> Buckets;
    public readonly ReadOnlySpan<Entry<TKey, TValue>> Entries;
    public readonly IEqualityComparer<TKey> Comparer;

    internal DictionaryInternalsRef(PooledDictionary<TKey, TValue> source)
    {
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        FastModMultiplier = source._fastModMultiplier;
#endif

        Count = source._count;
        FreeList = source._freeList;
        FreeCount = source._freeCount;
        Version = source._version;
        IsReferenceKey = PooledDictionary<TKey, TValue>.s_isReferenceKey;
        IsReferenceValue = PooledDictionary<TKey, TValue>.s_isReferenceValue;
        ClearEntries = PooledDictionary<TKey, TValue>.s_clearEntries;
        Buckets = source._buckets;
        Entries = source._entries;
        Comparer = source._comparer;
    }
}

partial class CollectionInternals
{
    /// <summary>Returns a structure that holds references to internal fields of <paramref name="source"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DictionaryInternalsRef<TKey, TValue> GetRef<TKey, TValue>(PooledDictionary<TKey, TValue> source)
        => new(source);

    /// <summary>Returns the internal <see cref="Entry{TKey, TValue}"/> array as a <see cref="ReadOnlySpan{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<Entry<TKey, TValue>> AsReadOnlySpan<TKey, TValue>(this PooledDictionary<TKey, TValue> source)
        => source._entries.AsSpan(0, source._count);

    /// <summary>Returns the internal <see cref="Entry{TKey, TValue}"/> array as a <see cref="ReadOnlyMemory{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<Entry<TKey, TValue>> AsReadOnlyMemory<TKey, TValue>(
        this PooledDictionary<TKey, TValue> source) => source._entries.AsMemory(0, source._count);
}