namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static class TempDictionary
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>()
        => new(0, null, ArrayPool<int>.Shared, ArrayPool<Entry<TKey, TValue>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(int capacity) => new(capacity,
        null,
        ArrayPool<int>.Shared,
        ArrayPool<Entry<TKey, TValue>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IEqualityComparer<TKey> comparer)
        => new(0, comparer, ArrayPool<int>.Shared, ArrayPool<Entry<TKey, TValue>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IDictionary<TKey, TValue> dictionary) => new(dictionary,
        null,
        ArrayPool<int>.Shared,
        ArrayPool<Entry<TKey, TValue>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> collection)
        => new(collection, null, ArrayPool<int>.Shared, ArrayPool<Entry<TKey, TValue>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(int capacity, IEqualityComparer<TKey> comparer)
        => new(capacity, comparer, ArrayPool<int>.Shared, ArrayPool<Entry<TKey, TValue>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IDictionary<TKey, TValue> dictionary,
        IEqualityComparer<TKey> comparer)
        => new(dictionary, comparer, ArrayPool<int>.Shared, ArrayPool<Entry<TKey, TValue>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> collection,
        IEqualityComparer<TKey> comparer)
        => new(collection, comparer, ArrayPool<int>.Shared, ArrayPool<Entry<TKey, TValue>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) => new(0, null, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(int capacity,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) => new(capacity, null, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) => new(0, comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IDictionary<TKey, TValue> dictionary,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) => new(dictionary, null, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> collection,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) => new(collection, null, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(int capacity,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) => new(capacity, comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IDictionary<TKey, TValue> dictionary,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) => new(dictionary, comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> collection,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) => new(collection, comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>((TKey Key, TValue Value)[] array,
        IEqualityComparer<TKey> comparer) => new(array.AsSpan(),
        comparer,
        ArrayPool<int>.Shared,
        ArrayPool<Entry<TKey, TValue>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>((TKey Key, TValue Value)[] array,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) => new(array.AsSpan(), comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(KeyValuePair<TKey, TValue>[] array,
        IEqualityComparer<TKey> comparer) => new(array.AsSpan(),
        comparer,
        ArrayPool<int>.Shared,
        ArrayPool<Entry<TKey, TValue>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(KeyValuePair<TKey, TValue>[] array,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) => new(array.AsSpan(), comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(ReadOnlySpan<(TKey Key, TValue Value)> span,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) => new(span, comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(ReadOnlySpan<KeyValuePair<TKey, TValue>> span,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) => new(span, comparer, bucketPool, entryPool);
}