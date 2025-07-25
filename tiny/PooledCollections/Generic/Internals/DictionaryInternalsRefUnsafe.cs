namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public readonly struct DictionaryInternalsRefUnsafe<TKey, TValue>
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

    public readonly int[] Buckets;
    public readonly Entry<TKey, TValue>[] Entries;
    public readonly IEqualityComparer<TKey> Comparer;

    internal DictionaryInternalsRefUnsafe(PooledDictionary<TKey, TValue> source)
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

partial class CollectionInternalsUnsafe
{
    /// <summary>Returns a structure that holds references to internal fields of <paramref name="source"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DictionaryInternalsRefUnsafe<TKey, TValue> GetRef<TKey, TValue>(PooledDictionary<TKey, TValue> source)
        => new(source);

    /// <summary>Returns the internal <see cref="Entry{TKey, TValue}"/> array as a <see cref="Span{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<Entry<TKey, TValue>> AsSpan<TKey, TValue>(this PooledDictionary<TKey, TValue> source)
        => source._entries.AsSpan(0, source._count);

    /// <summary>Returns the internal <see cref="Entry{TKey, TValue}"/> array as a <see cref="Memory{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<Entry<TKey, TValue>> AsMemory<TKey, TValue>(this PooledDictionary<TKey, TValue> source)
        => source._entries.AsMemory(0, source._count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<TKey, TValue>(this PooledDictionary<TKey, TValue> source,
        out Entry<TKey, TValue>[] entries,
        out int count)
    {
        entries = source._entries;
        count = source._count;
    }

    /// <summary>
    ///     Gets either a ref to a <typeparamref name="TValue"/> in the <see cref="PooledDictionary{TKey,TValue}"/> or a ref
    ///     null if it does not exist in the <paramref name="dictionary"/>.
    /// </summary>
    /// <param name="dictionary">The dictionary to get the ref to <typeparamref name="TValue"/> from.</param>
    /// <param name="key">The key used for lookup.</param>
    /// <remarks>
    ///     Items should not be added or removed from the <see cref="PooledDictionary{TKey,TValue}"/> while the ref
    ///     <typeparamref name="TValue"/> is in use. The ref null can be detected using
    ///     System.Runtime.CompilerServices.Unsafe.IsNullRef
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TValue GetValueRefOrNullRef<TKey, TValue>(PooledDictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull => ref dictionary.FindValue(key);

    /// <summary>
    ///     Gets a ref to a <typeparamref name="TValue"/> in the <see cref="PooledDictionary{TKey,TValue}"/>, adding a new
    ///     entry with a default value if it does not exist in the <paramref name="dictionary"/>.
    /// </summary>
    /// <param name="dictionary">The dictionary to get the ref to <typeparamref name="TValue"/> from.</param>
    /// <param name="key">The key used for lookup.</param>
    /// <param name="exists">Whether or not a new entry for the given key was added to the dictionary.</param>
    /// <remarks>
    ///     Items should not be added to or removed from the <see cref="PooledDictionary{TKey,TValue}"/> while the ref
    ///     <typeparamref name="TValue"/> is in use.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TValue?
        GetValueRefOrAddDefault<TKey, TValue>(PooledDictionary<TKey, TValue> dictionary, TKey key, out bool exists)
        where TKey : notnull
        => ref PooledDictionary<TKey, TValue>.CollectionsMarshalHelper.GetValueRefOrAddDefault(dictionary, key, out exists);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryInsert<TKey, TValue>(PooledDictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value,
        InsertionBehavior behavior) => dictionary.TryInsert(key, value, behavior);
}