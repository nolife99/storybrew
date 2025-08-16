namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct TempDictionaryInternalsRefUnsafe<TKey, TValue>
{
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
    public readonly ulong FastModMultiplier;
#endif

    public readonly int Count, FreeList, FreeCount, Version;
    public readonly bool IsReferenceKey, IsReferenceValue, ClearEntries;

    public readonly int[] Buckets;
    public readonly Entry<TKey, TValue>[] Entries;
    public readonly IEqualityComparer<TKey> Comparer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TempDictionaryInternalsRefUnsafe(scoped ref readonly TempDictionary<TKey, TValue> source)
    {
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        FastModMultiplier = source._fastModMultiplier;
#endif

        Count = source._count;
        FreeList = source._freeList;
        FreeCount = source._freeCount;
        Version = source._version;
        IsReferenceKey = TempDictionary<TKey, TValue>.s_isReferenceKey;
        IsReferenceValue = TempDictionary<TKey, TValue>.s_isReferenceValue;
        ClearEntries = TempDictionary<TKey, TValue>.s_clearEntries;
        Buckets = source._buckets;
        Entries = source._entries;
        Comparer = source._comparer;
    }
}

partial class CollectionInternals
{
    public static TempDictionaryInternalsRefUnsafe<TKey, TValue> GetUnsafeRef<TKey, TValue>(
        this scoped ref readonly TempDictionary<TKey, TValue> source)
        => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<Entry<TKey, TValue>> AsSpan<TKey, TValue>(
        this scoped ref readonly TempDictionary<TKey, TValue> source)
        => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(source._entries), source._count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<Entry<TKey, TValue>> AsMemory<TKey, TValue>(
        this scoped ref readonly TempDictionary<TKey, TValue> source)
        => new(source._entries, 0, source._count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<TKey, TValue>(this scoped ref readonly TempDictionary<TKey, TValue> source,
        out Entry<TKey, TValue>[] entries,
        out int count)
    {
        entries = source._entries;
        count = source._count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TValue GetValueRefOrAddDefault<TKey, TValue>(
        this scoped ref readonly TempDictionary<TKey, TValue> dictionary,
        TKey key,
        out bool exists) where TKey : notnull
        => ref TempDictionary<TKey, TValue>.CollectionsMarshalHelper.GetValueRefOrAddDefault(
            ref Unsafe.AsRef(in dictionary),
            key,
            out exists);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryInsert<TKey, TValue>(this scoped ref TempDictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value,
        InsertionBehavior behavior)
        => dictionary.TryInsert(key, value, behavior);
}