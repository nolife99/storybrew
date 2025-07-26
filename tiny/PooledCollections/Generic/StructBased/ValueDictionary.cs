// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/Dictionary.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic.StructBased;

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

public partial struct ValueDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
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

    internal static readonly IEqualityComparer<string> _stringComparer = PooledDictionary<string, byte>._stringComparer;

    internal readonly ArrayPool<Entry<TKey, TValue>> _entryPool;

    internal static readonly bool s_isReferenceKey = RuntimeHelpers.IsReferenceOrContainsReferences<TKey>();
    internal static readonly bool s_isReferenceValue = RuntimeHelpers.IsReferenceOrContainsReferences<TValue>();
    internal static readonly bool s_clearEntries = s_isReferenceKey || s_isReferenceValue;

    const int StartOfFreeList = -3;

    internal ValueDictionary(int capacity,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool)
    {
        _buckets = null;
        _entries = null;

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

        if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);

        if (capacity > 0) Initialize(capacity);
        else
        {
            _buckets = s_emptyBuckets;
            _entries = s_emptyEntries;
        }

        if (comparer is not null && comparer != EqualityComparer<TKey>.Default) _comparer = comparer;

        if (typeof(TKey) == typeof(string)) _comparer = (IEqualityComparer<TKey>)_stringComparer;
    }

    internal ValueDictionary(IDictionary<TKey, TValue> dictionary,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) : this(dictionary?.Count ?? 0, comparer, bucketPool, entryPool)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        AddRange(dictionary);
    }

    internal ValueDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection,
        IEqualityComparer<TKey> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<TKey, TValue>> entryPool) : this((collection as ICollection<KeyValuePair<TKey, TValue>>)?.Count ?? 0,
        comparer,
        bucketPool,
        entryPool)
    {
        ArgumentNullException.ThrowIfNull(collection);

        AddRange(collection);
    }

    void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> collection)
    {
        if (collection is ValueDictionary<TKey, TValue>)
        {
            var source = (ValueDictionary<TKey, TValue>)collection;

            if (source.Count == 0) return;

            Debug.Assert(source._entries is not null);
            Debug.Assert(_entries is not null);
            Debug.Assert(_entries.Length >= source.Count);
            Debug.Assert(_count == 0);

            var oldEntries = source._entries;
            if (source._comparer == _comparer)
            {
                CopyEntries(oldEntries, source._count);
                return;
            }

            var count = source._count;
            for (var i = 0; i < count; i++)

                if (oldEntries[i].Next >= -1)
                    Add(oldEntries[i].Key, oldEntries[i].Value);

            return;
        }

        foreach (var pair in collection) Add(pair.Key, pair.Value);
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

    public readonly ValueDictionaryKeyCollection<TKey, TValue> Keys => new(this);

    ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;

    public ValueDictionaryValueCollection<TKey, TValue> Values => new(this);

    ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

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
            Debug.Assert(modified);
        }
    }

    public void Add(TKey key, TValue value)
    {
        var modified = TryInsert(key, value, InsertionBehavior.ThrowOnExisting);
        Debug.Assert(modified);
    }

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> keyValuePair)
        => Add(keyValuePair.Key, keyValuePair.Value);

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> keyValuePair)
    {
        ref var value = ref FindValue(keyValuePair.Key);
        if (!Unsafe.IsNullRef(ref value) && EqualityComparer<TValue>.Default.Equals(value, keyValuePair.Value)) return true;

        return false;
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> keyValuePair)
    {
        ref var value = ref FindValue(keyValuePair.Key);
        if (!Unsafe.IsNullRef(ref value) && EqualityComparer<TValue>.Default.Equals(value, keyValuePair.Value))
        {
            Remove(keyValuePair.Key);
            return true;
        }

        return false;
    }

    public void Clear()
    {
        var count = _count;
        if (count > 0)
        {
            Debug.Assert(_buckets.IsNullOrEmpty() == false, "_buckets should be non-null");
            Debug.Assert(_entries is not null, "_entries should be non-null");

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
    public Enumerator GetEnumerator() => new(in this, Enumerator.KeyValuePair);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        => new Enumerator(this, Enumerator.KeyValuePair);

    internal ref TValue FindValue(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        ref var entry = ref Unsafe.NullRef<Entry<TKey, TValue>>();
        if (_buckets is not null)
        {
            Debug.Assert(_entries is not null, "expected entries to be is not null");
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
        Debug.Assert(_buckets.IsNullOrEmpty() == false);

        var entries = _entries;
        Debug.Assert(entries is not null, "expected entries to be non-null");

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
                        switch (behavior)
                        {
                            case InsertionBehavior.OverwriteExisting:
                                entries[i].Value = value;
                                return true;

                            case InsertionBehavior.ThrowOnExisting:
                                ThrowHelper.ThrowAddingDuplicateWithKeyArgumentException(key); break;
                        }

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
            Debug.Assert(StartOfFreeList - entries[_freeList].Next >= -1,
                "shouldn't overflow because `next` cannot underflow");

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

        if (!typeof(TKey).IsValueType &&
            collisionCount > HashHelpers.HashCollisionThreshold &&
            ReferenceEquals(comparer, _stringComparer)) Resize(entries.Length, true);

        return true;
    }

    internal static class CollectionsMarshalHelper
    {
        public static ref TValue GetValueRefOrAddDefault(scoped ref ValueDictionary<TKey, TValue> dictionary,
            TKey key,
            out bool exists)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (dictionary._buckets.IsNullOrEmpty()) dictionary.Initialize(0);
            Debug.Assert(dictionary._buckets.IsNullOrEmpty() == false);

            var entries = dictionary._entries;
            Debug.Assert(entries is not null, "expected entries to be non-null");

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

                        if (entries[i].HashCode == hashCode && EqualityComparer<TKey>.Default.Equals(entries[i].Key, key))
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
                Debug.Assert(StartOfFreeList - entries[dictionary._freeList].Next >= -1,
                    "shouldn't overflow because `next` cannot underflow");

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

            if (!typeof(TKey).IsValueType &&
                collisionCount > HashHelpers.HashCollisionThreshold &&
                ReferenceEquals(comparer, _stringComparer))
            {
                dictionary.Resize(entries.Length, true);

                exists = false;

                ref var value = ref dictionary.FindValue(key)!;

                Debug.Assert(!Unsafe.IsNullRef(ref value), "the lookup result cannot be a null ref here");

                return ref value;
            }

            exists = false;

            return ref entry.Value!;
        }
    }

    void Resize() => Resize(HashHelpers.ExpandPrime(_count), false);

    void Resize(int newSize, bool forceNewHashCodes)
    {
        Debug.Assert(!forceNewHashCodes || !typeof(TKey).IsValueType);
        Debug.Assert(_entries is not null, "_entries should be non-null");
        Debug.Assert(newSize >= _entries.Length);

        var count = _count;
        var entries = _entryPool.Rent(newSize);
        Array.Copy(_entries, entries, count);

        if (!typeof(TKey).IsValueType && forceNewHashCodes)
        {
            _comparer = EqualityComparer<TKey>.Default;

            for (var i = 0; i < count; i++)
                if (entries[i].Next >= -1)
                    entries[i].HashCode = (uint)_comparer.GetHashCode(entries[i].Key);

            if (ReferenceEquals(_comparer, EqualityComparer<TKey>.Default)) _comparer = null;
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

                if (entry.HashCode == hashCode &&
                    (_comparer?.Equals(entry.Key, key) ?? EqualityComparer<TKey>.Default.Equals(entry.Key, key)))
                {
                    if (last < 0) bucket = entry.Next + 1;
                    else entries[last].Next = entry.Next;

                    Debug.Assert(StartOfFreeList - _freeList < 0,
                        "shouldn't underflow because max hashtable length is MaxPrimeArrayLength = 0x7FEFFFFD(2146435069) _freelist underflow threshold 2147483646");

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

                if (entry.HashCode == hashCode &&
                    (_comparer?.Equals(entry.Key, key) ?? EqualityComparer<TKey>.Default.Equals(entry.Key, key)))
                {
                    if (last < 0) bucket = entry.Next + 1;
                    else entries[last].Next = entry.Next;

                    value = entry.Value;

                    Debug.Assert(StartOfFreeList - _freeList < 0,
                        "shouldn't underflow because max hashtable length is MaxPrimeArrayLength = 0x7FEFFFFD(2146435069) _freelist underflow threshold 2147483646");

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

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
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

    public bool TryAdd(TKey key, TValue value) => TryInsert(key, value, InsertionBehavior.None);

    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
        => CopyTo(array, index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this, Enumerator.KeyValuePair);

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
            if (entries[i].Next >= -1)
            {
                ref var entry = ref newEntries[newCount];
                entry = entries[i];
                ref var bucket = ref GetBucket(hashCode);
                entry.Next = bucket - 1;
                bucket = newCount + 1;
                newCount++;
            }
        }

        _count = newCount;
        _freeCount = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ref int GetBucket(uint hashCode)
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
        if (_buckets is not null)
            try
            {
                _bucketPool.Return(_buckets);
            }
            catch { }

        var buckets = _bucketPool.Rent(newSize);
        Array.Clear(buckets, 0, buckets.Length);
        _buckets = buckets;
    }

    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IDictionaryEnumerator
    {
        readonly ValueDictionary<TKey, TValue> _dictionary;
        readonly int _version;
        int _index;
        KeyValuePair<TKey, TValue> _current;
        readonly int _getEnumeratorRetType;

        internal const int DictEntry = 1;
        internal const int KeyValuePair = 2;

        public Enumerator(in ValueDictionary<TKey, TValue> dictionary, int getEnumeratorRetType)
        {
            _dictionary = dictionary;
            _version = dictionary._version;
            _index = 0;
            _getEnumeratorRetType = getEnumeratorRetType;
            _current = default;
        }

        public bool MoveNext()
        {
            if (_version != _dictionary._version)
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            while ((uint)_index < (uint)_dictionary._count)
            {
                ref var entry = ref _dictionary._entries![_index++];

                if (entry.Next >= -1)
                {
                    _current = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
                    return true;
                }
            }

            _index = _dictionary._count + 1;
            _current = default;
            return false;
        }

        public readonly KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _current;
        }

        public readonly void Dispose() { }

        readonly object IEnumerator.Current
        {
            get
            {
                if (_index == 0 || _index == _dictionary._count + 1)
                    ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();

                if (_getEnumeratorRetType == DictEntry) return new DictionaryEntry(_current.Key, _current.Value);

                return new KeyValuePair<TKey, TValue>(_current.Key, _current.Value);
            }
        }

        void IEnumerator.Reset()
        {
            if (_version != _dictionary._version)
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            _index = 0;
            _current = default;
        }

        readonly DictionaryEntry IDictionaryEnumerator.Entry
        {
            get
            {
                if (_index == 0 || _index == _dictionary._count + 1)
                    ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();

                return new DictionaryEntry(_current.Key, _current.Value);
            }
        }

        readonly object IDictionaryEnumerator.Key
        {
            get
            {
                if (_index == 0 || _index == _dictionary._count + 1)
                    ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();

                return _current.Key;
            }
        }

        readonly object IDictionaryEnumerator.Value
        {
            get
            {
                if (_index == 0 || _index == _dictionary._count + 1)
                    ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();

                return _current.Value;
            }
        }
    }
}