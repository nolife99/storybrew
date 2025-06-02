#pragma warning disable CS8632

namespace Tiny.PooledCollections.Generic;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

partial class PooledDictionary<TKey, TValue> : IDisposable
{
    public PooledDictionary((TKey Key, TValue Value)[] array, IEqualityComparer<TKey>? comparer) : this(array.AsSpan(),
        comparer,
        ArrayPool<int>.Shared,
        ArrayPool<Entry<TKey, TValue>>.Shared) { }

    public PooledDictionary((TKey Key, TValue Value)[] array,
        IEqualityComparer<TKey>? comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) : this(array.AsSpan(), comparer, bucketPool, entryPool) { }

    public PooledDictionary(KeyValuePair<TKey, TValue>[] array, IEqualityComparer<TKey>? comparer) : this(array.AsSpan(),
        comparer,
        ArrayPool<int>.Shared,
        ArrayPool<Entry<TKey, TValue>>.Shared) { }

    public PooledDictionary(KeyValuePair<TKey, TValue>[] array,
        IEqualityComparer<TKey>? comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) : this(array.AsSpan(), comparer, bucketPool, entryPool) { }

    public PooledDictionary(KVPair<TKey, TValue>[] array, IEqualityComparer<TKey>? comparer) : this(array.AsSpan(),
        comparer,
        ArrayPool<int>.Shared,
        ArrayPool<Entry<TKey, TValue>>.Shared) { }

    public PooledDictionary(KVPair<TKey, TValue>[] array,
        IEqualityComparer<TKey>? comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) : this(array.AsSpan(), comparer, bucketPool, entryPool) { }

    public PooledDictionary(ReadOnlySpan<(TKey Key, TValue Value)> span,
        IEqualityComparer<TKey>? comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) : this(span.Length, comparer, bucketPool, entryPool)
    {
        foreach (var pair in span) TryInsert(pair.Key, pair.Value, InsertionBehavior.ThrowOnExisting);
    }

    public PooledDictionary(ReadOnlySpan<KeyValuePair<TKey, TValue>> span,
        IEqualityComparer<TKey>? comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) : this(span.Length, comparer, bucketPool, entryPool)
    {
        foreach (var pair in span) TryInsert(pair.Key, pair.Value, InsertionBehavior.ThrowOnExisting);
    }

    public PooledDictionary(ReadOnlySpan<KVPair<TKey, TValue>> span,
        IEqualityComparer<TKey>? comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) : this(span.Length, comparer, bucketPool, entryPool)
    {
        foreach (var pair in span) TryInsert(pair.Key, pair.Value, InsertionBehavior.ThrowOnExisting);
    }

    public void Dispose()
    {
        ReturnBuckets(s_emptyBuckets);
        ReturnEntries(s_emptyEntries);
        _count = 0;
        _freeList = -1;
        _freeCount = 0;
        _version++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in Span<KeyValuePair<TKey, TValue>> dest) => CopyTo(dest, 0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in Span<KeyValuePair<TKey, TValue>> dest, int destIndex) => CopyTo(dest, destIndex, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(KVPair<TKey, TValue>[] dest) => CopyTo(dest, 0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(KVPair<TKey, TValue>[] dest, int destIndex) => CopyTo(dest, destIndex, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in Span<KVPair<TKey, TValue>> dest) => CopyTo(dest, 0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in Span<KVPair<TKey, TValue>> dest, int destIndex) => CopyTo(dest, destIndex, Count);

    public void CopyTo(in Span<KeyValuePair<TKey, TValue>> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        if (count < 0) ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_NeedNonNegNum();

        if (dest.Length - destIndex < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);

        var src = _entries.AsSpan(0, _count);

        if (src.Length == 0) return;

        for (int i = 0, len = src.Length; i < len && count > 0; i++)
        {
            ref var entry = ref src[i];
            if (entry.Next >= -1)
            {
                dest[destIndex++] = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
                count--;
            }
        }
    }

    public void CopyTo(KVPair<TKey, TValue>[] dest, int destIndex, int count)
    {
        if (dest == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dest);

        CopyTo(dest.AsSpan(), destIndex, count);
    }

    public void CopyTo(in Span<KVPair<TKey, TValue>> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        if (count < 0) ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_NeedNonNegNum();

        if (dest.Length - destIndex < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);

        var src = _entries.AsSpan();

        if (src.Length == 0) return;

        for (int i = 0, len = src.Length; i < len && count > 0; i++)
        {
            ref var entry = ref src[i];
            if (entry.Next >= -1)
            {
                dest[destIndex++] = new KVPair<TKey, TValue>(entry.Key, entry.Value);
                count--;
            }
        }
    }

    void ReturnBuckets(int[] replaceWith)
    {
        if (_buckets.IsNullOrEmpty() == false)
            try
            {
                _bucketPool.Return(_buckets);
            }
            catch { }

        _buckets = replaceWith ?? s_emptyBuckets;
    }

    void ReturnEntries(Entry<TKey, TValue>[] replaceWith)
    {
        if (_entries.IsNullOrEmpty() == false)
            try
            {
                _entryPool.Return(_entries, s_clearEntries);
            }
            catch { }

        _entries = replaceWith ?? s_emptyEntries;
    }
}