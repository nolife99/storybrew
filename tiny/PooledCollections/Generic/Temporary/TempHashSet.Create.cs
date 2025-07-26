namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static class TempHashSet
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>() => new(0, null, ArrayPool<int>.Shared, ArrayPool<Entry<T>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(int capacity)
        => new(capacity, null, ArrayPool<int>.Shared, ArrayPool<Entry<T>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(IEnumerable<T> collection)
        => new(collection, null, ArrayPool<int>.Shared, ArrayPool<Entry<T>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(IEqualityComparer<T> comparer)
        => new(comparer, ArrayPool<int>.Shared, ArrayPool<Entry<T>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(IEnumerable<T> collection, IEqualityComparer<T> comparer)
        => new(collection, comparer, ArrayPool<int>.Shared, ArrayPool<Entry<T>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(int capacity, IEqualityComparer<T> comparer)
        => new(capacity, comparer, ArrayPool<int>.Shared, ArrayPool<Entry<T>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool) => new(comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(IEnumerable<T> collection,
        IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool) => new(collection, comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(int capacity,
        IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool) => new(capacity, comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(T[] items) => new(items.AsSpan());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(T[] items, IEqualityComparer<T> comparer) => new(items.AsSpan(),
        comparer,
        ArrayPool<int>.Shared,
        ArrayPool<Entry<T>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(T[] items,
        IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool) => new(items.AsSpan(), comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(ReadOnlySpan<T> span) => new(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(ReadOnlySpan<T> span, IEqualityComparer<T> comparer)
        => new(span, comparer, ArrayPool<int>.Shared, ArrayPool<Entry<T>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(ReadOnlySpan<T> span,
        IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool) => new(span, comparer, bucketPool, entryPool);
}