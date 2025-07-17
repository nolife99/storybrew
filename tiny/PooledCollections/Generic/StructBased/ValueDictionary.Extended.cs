#pragma warning disable CS8632

namespace Tiny.PooledCollections.Generic.StructBased;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

partial struct ValueDictionary<TKey, TValue> : IDisposable
{
    internal ValueDictionary(ReadOnlySpan<(TKey Key, TValue Value)> span,
        IEqualityComparer<TKey>? comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) : this(span.Length, comparer, bucketPool, entryPool)
    {
        foreach (var pair in span) TryInsert(pair.Key, pair.Value, InsertionBehavior.ThrowOnExisting);
    }

    internal ValueDictionary(ReadOnlySpan<KeyValuePair<TKey, TValue>> span,
        IEqualityComparer<TKey>? comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) : this(span.Length, comparer, bucketPool, entryPool)
    {
        foreach (var pair in span) TryInsert(pair.Key, pair.Value, InsertionBehavior.ThrowOnExisting);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(scoped Span<KeyValuePair<TKey, TValue>> dest) => CopyTo(dest, 0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(scoped Span<KeyValuePair<TKey, TValue>> dest, int destIndex)
        => CopyTo(dest, destIndex, Count);

    public readonly void CopyTo(scoped Span<KeyValuePair<TKey, TValue>> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (dest.Length - destIndex < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);

        var src = _entries.AsSpan(0, _count);

        if (src.Length == 0) return;

        for (int i = 0, len = src.Length; i < len && count > 0; i++)
        {
            ref var entry = ref src[i];
            if (entry.Next < -1) continue;

            dest[destIndex++] = new(entry.Key, entry.Value);
            count--;
        }
    }

    void ReturnBuckets(int[] replaceWith)
    {
        if (_buckets is not null)
            try
            {
                _bucketPool.Return(_buckets);
            }
            catch { }

        _buckets = replaceWith ?? s_emptyBuckets;
    }

    void ReturnEntries(Entry<TKey, TValue>[] replaceWith)
    {
        if (_entries is not null) _entryPool.Return(_entries, s_clearEntries);

        _entries = replaceWith ?? s_emptyEntries;
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
}