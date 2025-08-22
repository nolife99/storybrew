// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/Dictionary.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

public static class TempDictionary
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>()
        => new(0, null, ArrayPool<int>.Shared, ArrayPool<Entry<TKey, TValue>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(int capacity)
        => new(capacity, null, ArrayPool<int>.Shared, ArrayPool<Entry<TKey, TValue>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IEqualityComparer<TKey> comparer)
        => new(0, comparer, ArrayPool<int>.Shared, ArrayPool<Entry<TKey, TValue>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IDictionary<TKey, TValue> dictionary)
        => new(dictionary, null, ArrayPool<int>.Shared, ArrayPool<Entry<TKey, TValue>>.Shared);

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
        ArrayPool<Entry<TKey, TValue>> entryPool)
        => new(0, null, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(int capacity,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool)
        => new(capacity, null, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool)
        => new(0, comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IDictionary<TKey, TValue> dictionary,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool)
        => new(dictionary, null, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> collection,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool)
        => new(collection, null, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(int capacity,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool)
        => new(capacity, comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IDictionary<TKey, TValue> dictionary,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool)
        => new(dictionary, comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> collection,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool)
        => new(collection, comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>((TKey Key, TValue Value)[] array,
        IEqualityComparer<TKey> comparer)
        => new(array.AsSpan(), comparer, ArrayPool<int>.Shared, ArrayPool<Entry<TKey, TValue>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>((TKey Key, TValue Value)[] array,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool)
        => new(array.AsSpan(), comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(KeyValuePair<TKey, TValue>[] array,
        IEqualityComparer<TKey> comparer)
        => new(array.AsSpan(), comparer, ArrayPool<int>.Shared, ArrayPool<Entry<TKey, TValue>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(KeyValuePair<TKey, TValue>[] array,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool)
        => new(array.AsSpan(), comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(ReadOnlySpan<(TKey Key, TValue Value)> span,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool)
        => new(span, comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempDictionary<TKey, TValue> Create<TKey, TValue>(ReadOnlySpan<KeyValuePair<TKey, TValue>> span,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool)
        => new(span, comparer, bucketPool, entryPool);
}

public ref struct TempDictionary<TKey, TValue>
{
    static readonly int[] s_emptyBuckets = [];
    static readonly Entry<TKey, TValue>[] s_emptyEntries = [];

    internal int[] _buckets;
    internal Entry<TKey, TValue>[] _entries;

#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
    internal ulong _fastModMultiplier;
#endif

    internal int _count;
    internal int _freeList;
    internal int _freeCount;
    internal int _version;
    internal IEqualityComparer<TKey> _comparer;

    internal readonly ArrayPool<int> _bucketPool;

    internal readonly ArrayPool<Entry<TKey, TValue>> _entryPool;

    internal static readonly bool s_isReferenceKey = RuntimeHelpers.IsReferenceOrContainsReferences<TKey>(),
        s_isReferenceValue = RuntimeHelpers.IsReferenceOrContainsReferences<TValue>(),
        s_clearEntries = s_isReferenceKey || s_isReferenceValue;

    const int StartOfFreeList = -3;

    internal TempDictionary(int capacity,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool)
    {
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        _fastModMultiplier = 0;
#endif

        _count = 0;
        _freeList = 0;
        _freeCount = 0;
        _version = 0;
        _comparer = null;

        _bucketPool = bucketPool ?? ArrayPool<int>.Shared;
        _entryPool = entryPool ?? ArrayPool<Entry<TKey, TValue>>.Shared;

        _buckets = s_emptyBuckets;
        _entries = s_emptyEntries;

        if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);

        if (capacity > 0) Initialize(capacity);
        else
        {
            _buckets = s_emptyBuckets;
            _entries = s_emptyEntries;
        }

        if (!typeof(TKey).IsValueType)
        {
            _comparer = comparer ?? EqualityComparer<TKey>.Default;

            if (typeof(TKey) == typeof(string) && _comparer!.GetStringComparer() is { } stringComparer)
                _comparer = (IEqualityComparer<TKey>)stringComparer;
        }
        else if (comparer is not null && !ReferenceEquals(comparer, EqualityComparer<TKey>.Default))
            _comparer = comparer;
    }

    internal TempDictionary(IDictionary<TKey, TValue> dictionary,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) : this(dictionary?.Count ?? 0, comparer, bucketPool, entryPool)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        AddRange(dictionary);
    }

    internal TempDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) : this(
        (collection as ICollection<KeyValuePair<TKey, TValue>>)?.Count ?? 0,
        comparer,
        bucketPool,
        entryPool)
    {
        ArgumentNullException.ThrowIfNull(collection);

        AddRange(collection);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> collection)
    {
        foreach (var pair in collection) Add(pair.Key, pair.Value);
    }

    public void AddRange(TempDictionary<TKey, TValue> collection)
    {
        if (collection.Count == 0) return;

        var oldEntries = collection._entries;
        if (ReferenceEquals(collection._comparer, _comparer))
        {
            CopyEntries(oldEntries, collection._count);
            return;
        }

        var count = collection._count;
        for (var i = 0; i < count; i++)

            if (oldEntries[i].Next >= -1)
                Add(oldEntries[i].Key, oldEntries[i].Value);
    }

    public readonly IEqualityComparer<TKey> Comparer => _comparer ?? EqualityComparer<TKey>.Default;

    public readonly int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count - _freeCount;
    }

    public readonly bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _entries is not null && _buckets is not null;
    }

    public TempDictionaryKeyCollection<TKey, TValue> Keys => new(this);

    public TempDictionaryValueCollection<TKey, TValue> Values => new(this);

    public TValue this[TKey key]
    {
        get
        {
            ref var value = ref FindValue(key);
            if (!Unsafe.IsNullRef(ref value)) return value;

            throw new KeyNotFoundException(nameof(key));
        }
        set
        {
            var modified = TryInsert(key, value, InsertionBehavior.OverwriteExisting);
        }
    }

    public void Add(TKey key, TValue value)
    {
        var modified = TryInsert(key, value, InsertionBehavior.ThrowOnExisting);
    }

    public void Clear()
    {
        var count = _count;
        if (count > 0)
        {
            Array.Clear(_buckets, 0, _buckets.Length);

            _count = 0;
            _freeList = -1;
            _freeCount = 0;
            Array.Clear(_entries, 0, count);
        }
    }

    public readonly bool ContainsKey(TKey key) => !Unsafe.IsNullRef(ref FindValue(key));

    public readonly bool ContainsValue(TValue value)
    {
        var entries = _entries;
        if (value is null)
        {
            for (var i = 0; i < _count; i++)
                if (entries![i].Next >= -1 && entries[i].Value is null)
                    return true;
        }
        else if (typeof(TValue).IsValueType)
        {
            for (var i = 0; i < _count; i++)
                if (entries![i].Next >= -1 && EqualityComparer<TValue>.Default.Equals(entries[i].Value, value))
                    return true;
        }
        else
        {
            var defaultComparer = EqualityComparer<TValue>.Default;
            for (var i = 0; i < _count; i++)
                if (entries![i].Next >= -1 && defaultComparer.Equals(entries[i].Value, value))
                    return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(KeyValuePair<TKey, TValue>[] dest) => CopyTo(dest, 0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(KeyValuePair<TKey, TValue>[] dest, int destIndex) => CopyTo(dest, destIndex, Count);

    public readonly void CopyTo(KeyValuePair<TKey, TValue>[] dest, int destIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(dest);

        CopyTo(dest.AsSpan(), destIndex, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Enumerator GetEnumerator() => new(in this);

    internal readonly ref TValue FindValue(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        ref var entry = ref Unsafe.NullRef<Entry<TKey, TValue>>();
        if (!_buckets.IsNullOrEmpty())
        {
            var comparer = _comparer;
            if (comparer is null)
            {
                var hashCode = (uint)key.GetHashCode();
                var i = GetBucket(hashCode);
                var entries = _entries;
                uint collisionCount = 0;
                if (typeof(TKey).IsValueType)
                {
                    i--;
                    do
                    {
                        if ((uint)i >= (uint)entries.Length) goto ReturnNotFound;

                        entry = ref entries[i];
                        if (entry.HashCode == hashCode && EqualityComparer<TKey>.Default.Equals(entry.Key, key))
                            goto ReturnFound;

                        i = entry.Next;

                        collisionCount++;
                    }
                    while (collisionCount <= (uint)entries.Length);

                    goto ConcurrentOperation;
                }

                var defaultComparer = EqualityComparer<TKey>.Default;

                i--;
                do
                {
                    if ((uint)i >= (uint)entries.Length) goto ReturnNotFound;

                    entry = ref entries[i];
                    if (entry.HashCode == hashCode && defaultComparer.Equals(entry.Key, key)) goto ReturnFound;

                    i = entry.Next;

                    collisionCount++;
                }
                while (collisionCount <= (uint)entries.Length);

                goto ConcurrentOperation;
            }
            else
            {
                var hashCode = (uint)comparer.GetHashCode(key);
                var i = GetBucket(hashCode);
                var entries = _entries;
                uint collisionCount = 0;
                i--;
                do
                {
                    if ((uint)i >= (uint)entries.Length) goto ReturnNotFound;

                    entry = ref entries[i];
                    if (entry.HashCode == hashCode && comparer.Equals(entry.Key, key)) goto ReturnFound;

                    i = entry.Next;

                    collisionCount++;
                }
                while (collisionCount <= (uint)entries.Length);

                goto ConcurrentOperation;
            }
        }

        goto ReturnNotFound;

    ConcurrentOperation:
        ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
    ReturnFound:
        ref var value = ref entry.Value;
    Return:
        return ref value;

    ReturnNotFound:
        value = ref Unsafe.NullRef<TValue>();
        goto Return;
    }

    int Initialize(int capacity)
    {
        var size = HashHelpers.GetPrime(capacity);
        var buckets = _bucketPool.Rent(size);
        var entries = _entryPool.Rent(size);

        _freeList = -1;

#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)size);
#endif

        Array.Clear(buckets, 0, buckets.Length);

        _buckets = buckets;
        _entries = entries;

        return size;
    }

    internal bool TryInsert(TKey key, TValue value, InsertionBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_buckets.IsNullOrEmpty()) Initialize(0);

        var entries = _entries;

        var comparer = _comparer;
        var hashCode = (uint)(comparer?.GetHashCode(key) ?? key.GetHashCode());

        uint collisionCount = 0;
        ref var bucket = ref GetBucket(hashCode);
        var i = bucket - 1;

        if (comparer is null)
        {
            if (typeof(TKey).IsValueType)

                while (true)
                {
                    if ((uint)i >= (uint)entries.Length) break;

                    if (entries[i].HashCode == hashCode && EqualityComparer<TKey>.Default.Equals(entries[i].Key, key))
                    {
                        if (behavior == InsertionBehavior.OverwriteExisting)
                        {
                            entries[i].Value = value;
                            return true;
                        }

                        if (behavior == InsertionBehavior.ThrowOnExisting)
                            ThrowHelper.ThrowAddingDuplicateWithKeyArgumentException(key);

                        return false;
                    }

                    i = entries[i].Next;

                    collisionCount++;
                    if (collisionCount > (uint)entries.Length)
                        ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }
            else
            {
                var defaultComparer = EqualityComparer<TKey>.Default;
                while (true)
                {
                    if ((uint)i >= (uint)entries.Length) break;

                    if (entries[i].HashCode == hashCode && defaultComparer.Equals(entries[i].Key, key))
                    {
                        if (behavior == InsertionBehavior.OverwriteExisting)
                        {
                            entries[i].Value = value;
                            return true;
                        }

                        if (behavior == InsertionBehavior.ThrowOnExisting)
                            ThrowHelper.ThrowAddingDuplicateWithKeyArgumentException(key);

                        return false;
                    }

                    i = entries[i].Next;

                    collisionCount++;
                    if (collisionCount > (uint)entries.Length)
                        ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }
            }
        }
        else
            while (true)
            {
                if ((uint)i >= (uint)entries.Length) break;

                if (entries[i].HashCode == hashCode && comparer.Equals(entries[i].Key, key))
                {
                    if (behavior == InsertionBehavior.OverwriteExisting)
                    {
                        entries[i].Value = value;
                        return true;
                    }

                    if (behavior == InsertionBehavior.ThrowOnExisting)
                        ThrowHelper.ThrowAddingDuplicateWithKeyArgumentException(key);

                    return false;
                }

                i = entries[i].Next;

                collisionCount++;
                if (collisionCount > (uint)entries.Length)
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
            }

        int index;
        if (_freeCount > 0)
        {
            index = _freeList;

            _freeList = StartOfFreeList - entries[_freeList].Next;
            _freeCount--;
        }
        else
        {
            var count = _count;
            if (count == entries.Length)
            {
                Resize();
                bucket = ref GetBucket(hashCode);
            }

            index = count;
            _count = count + 1;
            entries = _entries;
        }

        ref var entry = ref entries![index];
        entry.HashCode = hashCode;
        entry.Next = bucket - 1;
        entry.Key = key;
        entry.Value = value;
        bucket = index + 1;
        _version++;

        if (!typeof(TKey).IsValueType && collisionCount > HashHelpers.HashCollisionThreshold &&
            comparer.IsNonRandomizedStringComparer()) Resize(entries.Length, true);

        return true;
    }

    internal static class CollectionsMarshalHelper
    {
        public static ref TValue GetValueRefOrAddDefault(scoped ref TempDictionary<TKey, TValue> dictionary,
            TKey key,
            out bool exists)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (dictionary._buckets.IsNullOrEmpty()) dictionary.Initialize(0);

            var entries = dictionary._entries;

            var comparer = dictionary._comparer;
            var hashCode = (uint)(comparer?.GetHashCode(key) ?? key.GetHashCode());

            uint collisionCount = 0;
            ref var bucket = ref dictionary.GetBucket(hashCode);
            var i = bucket - 1;

            if (comparer is null)
            {
                if (typeof(TKey).IsValueType)

                    while (true)
                    {
                        if ((uint)i >= (uint)entries.Length) break;

                        if (entries[i].HashCode == hashCode &&
                            EqualityComparer<TKey>.Default.Equals(entries[i].Key, key))
                        {
                            exists = true;

                            return ref entries[i].Value!;
                        }

                        i = entries[i].Next;

                        collisionCount++;
                        if (collisionCount > (uint)entries.Length)
                            ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                    }
                else
                {
                    var defaultComparer = EqualityComparer<TKey>.Default;
                    while (true)
                    {
                        if ((uint)i >= (uint)entries.Length) break;

                        if (entries[i].HashCode == hashCode && defaultComparer.Equals(entries[i].Key, key))
                        {
                            exists = true;

                            return ref entries[i].Value!;
                        }

                        i = entries[i].Next;

                        collisionCount++;
                        if (collisionCount > (uint)entries.Length)
                            ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                    }
                }
            }
            else
                while (true)
                {
                    if ((uint)i >= (uint)entries.Length) break;

                    if (entries[i].HashCode == hashCode && comparer.Equals(entries[i].Key, key))
                    {
                        exists = true;

                        return ref entries[i].Value!;
                    }

                    i = entries[i].Next;

                    collisionCount++;
                    if (collisionCount > (uint)entries.Length)
                        ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }

            int index;
            if (dictionary._freeCount > 0)
            {
                index = dictionary._freeList;

                dictionary._freeList = StartOfFreeList - entries[dictionary._freeList].Next;
                dictionary._freeCount--;
            }
            else
            {
                var count = dictionary._count;
                if (count == entries.Length)
                {
                    dictionary.Resize();
                    bucket = ref dictionary.GetBucket(hashCode);
                }

                index = count;
                dictionary._count = count + 1;
                entries = dictionary._entries;
            }

            ref var entry = ref entries![index];
            entry.HashCode = hashCode;
            entry.Next = bucket - 1;
            entry.Key = key;
            entry.Value = default!;
            bucket = index + 1;
            dictionary._version++;

            if (!typeof(TKey).IsValueType && collisionCount > HashHelpers.HashCollisionThreshold &&
                comparer.IsNonRandomizedStringComparer())
            {
                dictionary.Resize(entries.Length, true);

                exists = false;

                ref var value = ref dictionary.FindValue(key)!;

                return ref value;
            }

            exists = false;

            return ref entry.Value!;
        }
    }

    void Resize() => Resize(HashHelpers.ExpandPrime(_count), false);

    void Resize(int newSize, bool forceNewHashCodes)
    {
        var count = _count;
        var entries = _entryPool.Rent(newSize);
        Array.Copy(_entries, entries, count);

        if (!typeof(TKey).IsValueType && forceNewHashCodes)
        {
            var comparer = _comparer = (IEqualityComparer<TKey>)((IEqualityComparer<string>)_comparer)
                .GetRandomizedStringComparer();

            for (var i = 0; i < count; i++)
                if (entries[i].Next >= -1)
                    entries[i].HashCode = (uint)comparer.GetHashCode(entries[i].Key);
        }

        RenewBuckets(newSize);

#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)newSize);
#endif

        for (var i = 0; i < count; i++)
            if (entries[i].Next >= -1)
            {
                ref var bucket = ref GetBucket(entries[i].HashCode);
                entries[i].Next = bucket - 1;
                bucket = i + 1;
            }

        _entryPool.Return(_entries, s_clearEntries);
        _entries = entries;
    }

    public bool Remove(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!_buckets.IsNullOrEmpty())
        {
            uint collisionCount = 0;
            var hashCode = (uint)(_comparer?.GetHashCode(key) ?? key.GetHashCode());
            ref var bucket = ref GetBucket(hashCode);
            var entries = _entries;
            var last = -1;
            var i = bucket - 1;
            while (i >= 0)
            {
                ref var entry = ref entries[i];

                if (entry.HashCode == hashCode && (_comparer?.Equals(entry.Key, key) ??
                    EqualityComparer<TKey>.Default.Equals(entry.Key, key)))
                {
                    if (last < 0) bucket = entry.Next + 1;
                    else entries[last].Next = entry.Next;

                    entry.Next = StartOfFreeList - _freeList;

                    if (s_isReferenceKey) entry.Key = default!;

                    if (s_isReferenceValue) entry.Value = default!;

                    _freeList = i;
                    _freeCount++;
                    return true;
                }

                last = i;
                i = entry.Next;

                collisionCount++;
                if (collisionCount > (uint)entries.Length)
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
            }
        }

        return false;
    }

    public bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!_buckets.IsNullOrEmpty())
        {
            uint collisionCount = 0;
            var hashCode = (uint)(_comparer?.GetHashCode(key) ?? key.GetHashCode());
            ref var bucket = ref GetBucket(hashCode);
            var entries = _entries;
            var last = -1;
            var i = bucket - 1;
            while (i >= 0)
            {
                ref var entry = ref entries[i];

                if (entry.HashCode == hashCode && (_comparer?.Equals(entry.Key, key) ??
                    EqualityComparer<TKey>.Default.Equals(entry.Key, key)))
                {
                    if (last < 0) bucket = entry.Next + 1;
                    else entries[last].Next = entry.Next;

                    value = entry.Value;

                    entry.Next = StartOfFreeList - _freeList;

                    if (s_isReferenceKey) entry.Key = default!;

                    if (s_isReferenceValue) entry.Value = default!;

                    _freeList = i;
                    _freeCount++;
                    return true;
                }

                last = i;
                i = entry.Next;

                collisionCount++;
                if (collisionCount > (uint)entries.Length)
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
            }
        }

        value = default;
        return false;
    }

    public readonly bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        ref var valRef = ref FindValue(key);
        if (!Unsafe.IsNullRef(ref valRef))
        {
            value = valRef;
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(TKey key, TValue value) => TryInsert(key, value, InsertionBehavior.None);

    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);

        var currentCapacity = _entries?.Length ?? 0;
        if (currentCapacity >= capacity) return currentCapacity;

        _version++;

        if (_buckets.IsNullOrEmpty()) return Initialize(capacity);

        var newSize = HashHelpers.GetPrime(capacity);
        Resize(newSize, false);
        return newSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrimExcess() => TrimExcess(Count);

    public void TrimExcess(int capacity)
    {
        if (capacity < Count) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);

        var newSize = HashHelpers.GetPrime(capacity);
        var oldEntries = _entries;
        var currentCapacity = oldEntries?.Length ?? 0;
        if (newSize >= currentCapacity) return;

        var oldBuckets = _buckets;

        var oldCount = _count;
        _version++;
        Initialize(newSize);

        CopyEntries(oldEntries, oldCount);

        _bucketPool.Return(oldBuckets);
        _entryPool.Return(oldEntries, s_clearEntries);
    }

    void CopyEntries(Entry<TKey, TValue>[] entries, int count)
    {
        var newEntries = _entries;
        var newCount = 0;
        for (var i = 0; i < count; i++)
        {
            var hashCode = entries[i].HashCode;
            if (entries[i].Next < -1) continue;

            ref var entry = ref newEntries[newCount];
            entry = entries[i];
            ref var bucket = ref GetBucket(hashCode);
            entry.Next = bucket - 1;
            bucket = newCount + 1;
            newCount++;
        }

        _count = newCount;
        _freeCount = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    readonly ref int GetBucket(uint hashCode)
    {
        var buckets = _buckets!;
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        return ref buckets[HashHelpers.FastMod(hashCode, (uint)buckets.Length, _fastModMultiplier)];
#else
        return ref buckets[hashCode % (uint)buckets.Length];
#endif
    }

    void RenewBuckets(int newSize)
    {
        if (_buckets is not null) _bucketPool.Return(_buckets);

        var buckets = _bucketPool.Rent(newSize);
        Array.Clear(buckets, 0, buckets.Length);
        _buckets = buckets;
    }

    public ref struct Enumerator
    {
        readonly TempDictionary<TKey, TValue> _dictionary;
        readonly int _version;
        int _index;

        internal const int KeyValuePair = 2;

        internal Enumerator(scoped ref readonly TempDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            _version = dictionary._version;
            _index = 0;
            Current = default;
        }

        public bool MoveNext()
        {
            if (_version != _dictionary._version)
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            while ((uint)_index < (uint)_dictionary._count)
            {
                ref var entry = ref _dictionary._entries![_index++];

                if (entry.Next < -1) continue;

                Current = new(entry.Key, entry.Value);
                return true;
            }

            _index = _dictionary._count + 1;
            Current = default;
            return false;
        }

        public KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            private set;
        }
    }

    internal TempDictionary(ReadOnlySpan<(TKey Key, TValue Value)> span,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) : this(span.Length, comparer, bucketPool, entryPool)
    {
        foreach (var pair in span) TryInsert(pair.Key, pair.Value, InsertionBehavior.ThrowOnExisting);
    }

    internal TempDictionary(ReadOnlySpan<KeyValuePair<TKey, TValue>> span,
        IEqualityComparer<TKey> comparer,
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

        if (dest.Length - destIndex < count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);

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
        if (_buckets is not null) _bucketPool.Return(_buckets);

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

public readonly ref struct TempDictionaryKeyCollection<TKey, TValue>
{
    readonly TempDictionary<TKey, TValue> _dictionary;

    internal TempDictionaryKeyCollection(TempDictionary<TKey, TValue> dictionary) => _dictionary = dictionary;

    public Enumerator GetEnumerator() => new(_dictionary);

    public void CopyTo(TKey[] array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (index < 0 || index > array.Length) ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();

        if (array.Length - index < _dictionary.Count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);

        var count = _dictionary._count;
        var entries = _dictionary._entries;
        for (var i = 0; i < count; i++)
            if (entries![i].Next >= -1)
                array[index++] = entries[i].Key;
    }

    public int Count => _dictionary.Count;

    public ref struct Enumerator
    {
        readonly TempDictionary<TKey, TValue> _dictionary;
        int _index;
        readonly int _version;

        internal Enumerator(TempDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            _version = dictionary._version;
            _index = 0;
            Current = default;
        }

        public readonly void Dispose() { }

        public bool MoveNext()
        {
            if (_version != _dictionary._version)
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            while ((uint)_index < (uint)_dictionary._count)
            {
                ref var entry = ref _dictionary._entries![_index++];

                if (entry.Next < -1) continue;

                Current = entry.Key;
                return true;
            }

            _index = _dictionary._count + 1;
            Current = default;
            return false;
        }

        public TKey Current { get; private set; }
    }
}

public readonly ref struct TempDictionaryValueCollection<TKey, TValue>
{
    readonly TempDictionary<TKey, TValue> _dictionary;

    internal TempDictionaryValueCollection(TempDictionary<TKey, TValue> dictionary) => _dictionary = dictionary;

    public Enumerator GetEnumerator() => new(_dictionary);

    public void CopyTo(TValue[] array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);

        if ((uint)index > array.Length) ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();

        if (array.Length - index < _dictionary.Count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);

        var count = _dictionary._count;
        var entries = _dictionary._entries;
        for (var i = 0; i < count; i++)
            if (entries![i].Next >= -1)
                array[index++] = entries[i].Value;
    }

    public int Count => _dictionary.Count;

    public ref struct Enumerator
    {
        readonly TempDictionary<TKey, TValue> _dictionary;
        int _index;
        readonly int _version;

        internal Enumerator(TempDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            _version = dictionary._version;
            _index = 0;
            Current = default;
        }

        public readonly void Dispose() { }

        public bool MoveNext()
        {
            if (_version != _dictionary._version)
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            while ((uint)_index < (uint)_dictionary._count)
            {
                ref var entry = ref _dictionary._entries![_index++];

                if (entry.Next < -1) continue;

                Current = entry.Value;
                return true;
            }

            _index = _dictionary._count + 1;
            Current = default;
            return false;
        }

        public TValue Current { get; private set; }
    }
}