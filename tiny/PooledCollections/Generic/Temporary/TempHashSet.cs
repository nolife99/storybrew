// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/HashSet.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
        ArrayPool<Entry<T>> entryPool)
        => new(comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(IEnumerable<T> collection,
        IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool)
        => new(collection, comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(int capacity,
        IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool)
        => new(capacity, comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(T[] items) => new(items.AsSpan());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(T[] items, IEqualityComparer<T> comparer)
        => new(items.AsSpan(), comparer, ArrayPool<int>.Shared, ArrayPool<Entry<T>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(T[] items,
        IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool)
        => new(items.AsSpan(), comparer, bucketPool, entryPool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(ReadOnlySpan<T> span) => new(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(ReadOnlySpan<T> span, IEqualityComparer<T> comparer)
        => new(span, comparer, ArrayPool<int>.Shared, ArrayPool<Entry<T>>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempHashSet<T> Create<T>(ReadOnlySpan<T> span,
        IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool)
        => new(span, comparer, bucketPool, entryPool);
}

public ref struct TempHashSet<T>
{
    const int StackAllocThreshold = 100;

    const int ShrinkThreshold = 3;

    const int StartOfFreeList = -3;

    static readonly int[] s_emptyBuckets = [];
    static readonly Entry<T>[] s_emptyEntries = [];

    internal static readonly bool s_clearEntries = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    internal int[] _buckets;
    internal Entry<T>[] _entries;

#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
    internal ulong _fastModMultiplier;
#endif
    internal int _count;
    internal int _freeList;
    internal int _freeCount;
    internal int _version;
    internal IEqualityComparer<T> _comparer;

    internal readonly ArrayPool<int> _bucketPool;

    internal readonly ArrayPool<Entry<T>> _entryPool;

    #region Constructors

    internal TempHashSet(IEqualityComparer<T> comparer, ArrayPool<int> bucketPool, ArrayPool<Entry<T>> entryPool)
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
        _entryPool = entryPool ?? ArrayPool<Entry<T>>.Shared;

        _buckets = s_emptyBuckets;
        _entries = s_emptyEntries;

        if (!typeof(T).IsValueType)
        {
            _comparer = comparer ?? EqualityComparer<T>.Default;

            if (typeof(T) == typeof(string) && _comparer.GetStringComparer() is { } stringComparer)
                _comparer = (IEqualityComparer<T>)stringComparer;
        }
        else if (comparer is not null && !ReferenceEquals(comparer, EqualityComparer<T>.Default)) _comparer = comparer;
    }

    internal TempHashSet(IEnumerable<T> collection,
        IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool) : this(comparer, bucketPool, entryPool)
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (collection is ICollection<T> coll)
        {
            var count = coll.Count;
            if (count > 0) Initialize(count);
        }

        UnionWith(collection);

        if (_count > 0 && _entries!.Length / _count > ShrinkThreshold) TrimExcess();
    }

    internal TempHashSet(int capacity,
        IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool) : this(comparer, bucketPool, entryPool)
    {
        if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);

        if (capacity > 0) Initialize(capacity);
    }

    #endregion

    #region ICollection<T> methods

    public void Clear()
    {
        var count = _count;
        if (count <= 0) return;

        Debug.Assert(!_buckets.IsNullOrEmpty(), "_buckets should be non-null");
        Debug.Assert(_entries is not null, "_entries should be non-null");

        Array.Clear(_buckets, 0, _buckets.Length);
        _count = 0;
        _freeList = -1;
        _freeCount = 0;
        Array.Clear(_entries, 0, count);
    }

    public bool Contains(T item) => FindItemIndex(item) >= 0;

    readonly int FindItemIndex(T item)
    {
        var buckets = _buckets;
        if (buckets is null) return -1;

        var entries = _entries;
        Debug.Assert(entries is not null, "Expected _entries to be initialized");

        uint collisionCount = 0;
        var comparer = _comparer;

        if (comparer is null)
        {
            var hashCode = item is not null ? item.GetHashCode() : 0;
            if (typeof(T).IsValueType)
            {
                var i = GetBucketRef(hashCode) - 1;
                while (i >= 0)
                {
                    ref var entry = ref entries[i];
                    if (entry.HashCode == hashCode && EqualityComparer<T>.Default.Equals(entry.Value, item)) return i;

                    i = entry.Next;

                    collisionCount++;
                    if (collisionCount > (uint)entries.Length)
                        ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }
            }
            else
            {
                var defaultComparer = EqualityComparer<T>.Default;
                var i = GetBucketRef(hashCode) - 1;
                while (i >= 0)
                {
                    ref var entry = ref entries[i];
                    if (entry.HashCode == hashCode && defaultComparer.Equals(entry.Value, item)) return i;

                    i = entry.Next;

                    collisionCount++;
                    if (collisionCount > (uint)entries.Length)
                        ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }
            }
        }
        else
        {
            var hashCode = item is not null ? comparer.GetHashCode(item) : 0;
            var i = GetBucketRef(hashCode) - 1;
            while (i >= 0)
            {
                ref var entry = ref entries[i];
                if (entry.HashCode == hashCode && comparer.Equals(entry.Value, item)) return i;

                i = entry.Next;

                collisionCount++;
                if (collisionCount > (uint)entries.Length)
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
            }
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    readonly ref int GetBucketRef(int hashCode)
    {
        var buckets = _buckets!;
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        return ref buckets[HashHelpers.FastMod((uint)hashCode, (uint)buckets.Length, _fastModMultiplier)];
#else
        return ref buckets[(uint)hashCode % (uint)buckets.Length];
#endif
    }

    public bool Remove(T item)
    {
        if (_buckets is null) return false;

        var entries = _entries;
        Debug.Assert(entries is not null, "entries should be non-null");

        uint collisionCount = 0;
        var last = -1;
        var hashCode = item is not null ? _comparer?.GetHashCode(item) ?? item.GetHashCode() : 0;

        ref var bucket = ref GetBucketRef(hashCode);
        var i = bucket - 1;

        while (i >= 0)
        {
            ref var entry = ref entries[i];

            if (entry.HashCode == hashCode && (_comparer?.Equals(entry.Value, item) ??
                EqualityComparer<T>.Default.Equals(entry.Value, item)))
            {
                if (last < 0) bucket = entry.Next + 1;
                else entries[last].Next = entry.Next;

                Debug.Assert(StartOfFreeList - _freeList < 0,
                    "shouldn't underflow because max hashtable length is MaxPrimeArrayLength = 0x7FEFFFFD(2146435069) _freelist underflow threshold 2147483646");

                entry.Next = StartOfFreeList - _freeList;

                if (s_clearEntries) entry.Value = default!;

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

        return false;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count - _freeCount;
    }

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _entries is not null && _buckets is not null;
    }

    #endregion

    #region IEnumerable methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(this);

    #endregion

    #region HashSet methods

    public bool Add(T item) => AddIfNotPresent(item, out _);

    public bool TryGetValue(T equalValue, [MaybeNullWhen(false)] out T actualValue)
    {
        if (_buckets is not null)
        {
            var index = FindItemIndex(equalValue);
            if (index >= 0)
            {
                actualValue = _entries![index].Value;
                return true;
            }
        }

        actualValue = default;
        return false;
    }

    public void UnionWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var item in other) AddIfNotPresent(item, out _);
    }

    public void IntersectWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0) return;

        if (other is ICollection<T> otherAsCollection)
        {
            if (otherAsCollection.Count == 0)
            {
                Clear();
                return;
            }

            if (other is HashSet<T> otherAsSCGSet && EqualityComparersAreEqual(this, otherAsSCGSet))
            {
                IntersectWithHashSetWithSameComparer(otherAsSCGSet);
                return;
            }
        }

        IntersectWithEnumerable(other);
    }

    public void ExceptWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0) return;

        foreach (var element in other) Remove(element);
    }

    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0)
        {
            UnionWith(other);
            return;
        }

        if (other is HashSet<T> otherAsSCGSet && EqualityComparersAreEqual(this, otherAsSCGSet))
            SymmetricExceptWithUniqueHashSet(otherAsSCGSet);
        else SymmetricExceptWithEnumerable(other);
    }

    public bool IsSubsetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0) return true;

        if (other is HashSet<T> otherAsSCGSet && EqualityComparersAreEqual(this, otherAsSCGSet))
            return Count > otherAsSCGSet.Count ? false : IsSubsetOfHashSetWithSameComparer(otherAsSCGSet);

        var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, false);
        return uniqueCount == Count && unfoundCount >= 0;
    }

    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (other is ICollection<T> otherAsCollection)
        {
            if (otherAsCollection.Count == 0) return false;

            if (Count == 0) return otherAsCollection.Count > 0;

            if (other is HashSet<T> otherAsSCGSet && EqualityComparersAreEqual(this, otherAsSCGSet))
                return Count >= otherAsSCGSet.Count ? false : IsSubsetOfHashSetWithSameComparer(otherAsSCGSet);
        }

        var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, false);
        return uniqueCount == Count && unfoundCount > 0;
    }

    public bool IsSupersetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return other is not ICollection<T> otherAsCollection ? ContainsAllElements(other) :
            otherAsCollection.Count == 0 ? true :
            other is HashSet<T> otherAsSCGSet && EqualityComparersAreEqual(this, otherAsSCGSet) &&
            otherAsSCGSet.Count > Count ? false : ContainsAllElements(other);
    }

    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0) return false;

        if (other is ICollection<T> otherAsCollection)
        {
            if (otherAsCollection.Count == 0) return true;

            if (other is HashSet<T> otherAsSCGSet && EqualityComparersAreEqual(this, otherAsSCGSet))
                return otherAsSCGSet.Count >= Count ? false : ContainsAllElements(otherAsSCGSet);
        }

        var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, true);
        return uniqueCount < Count && unfoundCount == 0;
    }

    public bool Overlaps(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0) return false;

        foreach (var element in other)
            if (Contains(element))
                return true;

        return false;
    }

    public bool SetEquals(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (other is HashSet<T> otherAsSCGSet && EqualityComparersAreEqual(this, otherAsSCGSet))
            return Count != otherAsSCGSet.Count ? false : ContainsAllElements(otherAsSCGSet);

        if (Count == 0 && other is ICollection<T> { Count: > 0 }) return false;

        var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, true);
        return uniqueCount == Count && unfoundCount == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest) => CopyTo(dest, 0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest, int destIndex) => CopyTo(dest, destIndex, Count);

    public void CopyTo(T[] dest, int destIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(dest);

        CopyTo(dest.AsSpan(), destIndex, count);
    }

    public int RemoveWhere(Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var entries = _entries;
        var numRemoved = 0;
        for (var i = 0; i < _count; i++)
        {
            ref var entry = ref entries![i];
            if (entry.Next < -1) continue;

            var value = entry.Value;
            if (!match(value)) continue;

            if (Remove(value)) numRemoved++;
        }

        return numRemoved;
    }

    public IEqualityComparer<T> Comparer => _comparer ?? EqualityComparer<T>.Default;

    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);

        var currentCapacity = _entries?.Length ?? 0;
        if (currentCapacity >= capacity) return currentCapacity;

        if (_buckets.IsNullOrEmpty()) return Initialize(capacity);

        var newSize = HashHelpers.GetPrime(capacity);
        Resize(newSize, false);
        return newSize;
    }

    void Resize() => Resize(HashHelpers.ExpandPrime(_count), false);

    void Resize(int newSize, bool forceNewHashCodes)
    {
        Debug.Assert(!forceNewHashCodes || !typeof(T).IsValueType);
        Debug.Assert(_entries is not null, "_entries should be non-null");
        Debug.Assert(newSize >= _entries.Length);

        var count = _count;
        var entries = _entryPool.Rent(newSize);
        Array.Copy(_entries, entries, count);

        if (!typeof(T).IsValueType && forceNewHashCodes)
        {
            var comparer = _comparer = (IEqualityComparer<T>)((IEqualityComparer<string>)_comparer)
                .GetRandomizedStringComparer();

            for (var i = 0; i < count; i++)
            {
                ref var entry = ref entries[i];
                if (entry.Next >= -1) entry.HashCode = entry.Value is null ? 0 : comparer.GetHashCode(entry.Value);
            }
        }

        RenewBuckets(newSize);

#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)newSize);
#endif
        for (var i = 0; i < count; i++)
        {
            ref var entry = ref entries[i];
            if (entry.Next < -1) continue;

            ref var bucket = ref GetBucketRef(entry.HashCode);
            entry.Next = bucket - 1;
            bucket = i + 1;
        }

        _entryPool.Return(_entries, s_clearEntries);
        _entries = entries;
    }

    public void TrimExcess()
    {
        var capacity = Count;

        var newSize = HashHelpers.GetPrime(capacity);
        var oldEntries = _entries;
        var currentCapacity = oldEntries?.Length ?? 0;
        if (newSize >= currentCapacity) return;

        var oldBuckets = _buckets;

        var oldCount = _count;
        _version++;
        Initialize(newSize);
        var entries = _entries;
        var count = 0;
        for (var i = 0; i < oldCount; i++)
        {
            var hashCode = oldEntries![i].HashCode;
            if (oldEntries[i].Next < -1) continue;

            ref var entry = ref entries![count];
            entry = oldEntries[i];
            ref var bucket = ref GetBucketRef(hashCode);
            entry.Next = bucket - 1;
            bucket = count + 1;
            count++;
        }

        _count = capacity;
        _freeCount = 0;

        _bucketPool.Return(oldBuckets);
        _entryPool.Return(oldEntries, s_clearEntries);
    }

    #endregion

    #region Helper methods

    int Initialize(int capacity)
    {
        var size = HashHelpers.GetPrime(capacity);

        var buckets = _bucketPool.Rent(size);
        Array.Clear(buckets, 0, buckets.Length);
        _buckets = buckets;

        _entries = _entryPool.Rent(size);

        _freeList = -1;

#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)size);
#endif

        return size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Entry<T>[] GetEntries() => _entries ?? s_emptyEntries;

    internal bool AddIfNotPresent(T value, out int location)
    {
        if (_buckets.IsNullOrEmpty()) Initialize(0);
        Debug.Assert(!_buckets.IsNullOrEmpty());

        var entries = _entries;
        Debug.Assert(entries is not null, "expected entries to be non-null");

        var comparer = _comparer;
        int hashCode;

        uint collisionCount = 0;
        ref var bucket = ref Unsafe.NullRef<int>();

        if (comparer is null)
        {
            hashCode = value is not null ? value.GetHashCode() : 0;
            bucket = ref GetBucketRef(hashCode);
            var i = bucket - 1;
            if (typeof(T).IsValueType)

                while (i >= 0)
                {
                    ref var entry = ref entries[i];
                    if (entry.HashCode == hashCode && EqualityComparer<T>.Default.Equals(entry.Value, value))
                    {
                        location = i;
                        return false;
                    }

                    i = entry.Next;

                    collisionCount++;
                    if (collisionCount > (uint)entries.Length)
                        ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }
            else
            {
                var defaultComparer = EqualityComparer<T>.Default;
                while (i >= 0)
                {
                    ref var entry = ref entries[i];
                    if (entry.HashCode == hashCode && defaultComparer.Equals(entry.Value, value))
                    {
                        location = i;
                        return false;
                    }

                    i = entry.Next;

                    collisionCount++;
                    if (collisionCount > (uint)entries.Length)
                        ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }
            }
        }
        else
        {
            hashCode = value is not null ? comparer.GetHashCode(value) : 0;
            bucket = ref GetBucketRef(hashCode);
            var i = bucket - 1;
            while (i >= 0)
            {
                ref var entry = ref entries[i];
                if (entry.HashCode == hashCode && comparer.Equals(entry.Value, value))
                {
                    location = i;
                    return false;
                }

                i = entry.Next;

                collisionCount++;
                if (collisionCount > (uint)entries.Length)
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
            }
        }

        int index;
        if (_freeCount > 0)
        {
            index = _freeList;
            _freeCount--;
            Debug.Assert(StartOfFreeList - entries![_freeList].Next >= -1,
                "shouldn't overflow because `next` cannot underflow");

            _freeList = StartOfFreeList - entries[_freeList].Next;
        }
        else
        {
            var count = _count;
            if (count == entries.Length)
            {
                Resize();
                bucket = ref GetBucketRef(hashCode);
            }

            index = count;
            _count = count + 1;
            entries = _entries;
        }

        {
            ref var entry = ref entries![index];
            entry.HashCode = hashCode;
            entry.Next = bucket - 1;
            entry.Value = value;
            bucket = index + 1;
            _version++;
            location = index;
        }

        if (typeof(T).IsValueType || collisionCount <= HashHelpers.HashCollisionThreshold ||
            !comparer.IsNonRandomizedStringComparer()) return true;

        Resize(entries.Length, true);
        location = FindItemIndex(value);
        Debug.Assert(location >= 0);

        return true;
    }

    bool ContainsAllElements(IEnumerable<T> other)
    {
        foreach (var element in other)
            if (!Contains(element))
                return false;

        return true;
    }

    internal bool IsSubsetOfHashSetWithSameComparer(TempHashSet<T> other)
    {
        var entries = _entries;
        for (var i = 0; i < _count; i++)
        {
            ref var entry = ref entries![i];
            if (entry.Next < -1) continue;

            var item = entry.Value;
            if (!other.Contains(item)) return false;
        }

        return true;
    }

    internal bool IsSubsetOfHashSetWithSameComparer(HashSet<T> other)
    {
        var entries = _entries;
        for (var i = 0; i < _count; i++)
        {
            ref var entry = ref entries![i];
            if (entry.Next < -1) continue;

            var item = entry.Value;
            if (!other.Contains(item)) return false;
        }

        return true;
    }

    internal void IntersectWithHashSetWithSameComparer(TempHashSet<T> other)
    {
        var entries = _entries;
        for (var i = 0; i < _count; i++)
        {
            ref var entry = ref entries![i];
            if (entry.Next < -1) continue;

            var item = entry.Value;
            if (!other.Contains(item)) Remove(item);
        }
    }

    internal void IntersectWithHashSetWithSameComparer(HashSet<T> other)
    {
        var entries = _entries;
        for (var i = 0; i < _count; i++)
        {
            ref var entry = ref entries![i];
            if (entry.Next < -1) continue;

            var item = entry.Value;
            if (!other.Contains(item)) Remove(item);
        }
    }

    void IntersectWithEnumerable(IEnumerable<T> other)
    {
        Debug.Assert(!_buckets.IsNullOrEmpty(), "_buckets shouldn't be null; callers should check first");

        var originalCount = _count;
        var intArrayLength = BitHelper.ToIntArrayLength(originalCount);

        int[] pooledArray = null;
        BitHelper bitHelper = new(intArrayLength <= StackAllocThreshold ?
                stackalloc int[intArrayLength] :
                new(pooledArray = _bucketPool.Rent(intArrayLength), 0, 100),
            true);

        foreach (var item in other)
        {
            var index = FindItemIndex(item);
            if (index >= 0) bitHelper.MarkBit(index);
        }

        for (var i = 0; i < originalCount; i++)
        {
            ref var entry = ref _entries![i];
            if (entry.Next >= -1 && !bitHelper.IsMarked(i)) Remove(entry.Value);
        }

        if (pooledArray is not null) _bucketPool.Return(pooledArray);
    }

    void SymmetricExceptWithUniqueHashSet(HashSet<T> other)
    {
        foreach (var item in other)
            if (!Remove(item))
                AddIfNotPresent(item, out _);
    }

    void SymmetricExceptWithEnumerable(IEnumerable<T> other)
    {
        var originalCount = _count;
        var intArrayLength = BitHelper.ToIntArrayLength(originalCount);

        int[] itemsToRemoveArray = null;
        BitHelper itemsToRemove = new(intArrayLength <= StackAllocThreshold / 2 ?
                stackalloc int[intArrayLength] :
                new(itemsToRemoveArray = _bucketPool.Rent(intArrayLength), 0, 100),
            true);

        int[] itemsAddedFromOtherArray = null;
        BitHelper itemsAddedFromOther = new(itemsToRemoveArray is null ?
                stackalloc int[intArrayLength] :
                new(itemsAddedFromOtherArray = _bucketPool.Rent(intArrayLength), 0, 100),
            true);

        foreach (var item in other)
            if (AddIfNotPresent(item, out var location)) itemsAddedFromOther.MarkBit(location);
            else
            {
                if (location < originalCount && !itemsAddedFromOther.IsMarked(location))
                    itemsToRemove.MarkBit(location);
            }

        for (var i = 0; i < originalCount; i++)
            if (itemsToRemove.IsMarked(i))
                Remove(_entries![i].Value);

        if (itemsToRemoveArray is null) return;

        _bucketPool.Return(itemsToRemoveArray);
        _bucketPool.Return(itemsAddedFromOtherArray);
    }

    (int UniqueCount, int UnfoundCount) CheckUniqueAndUnfoundElements(IEnumerable<T> other, bool returnIfUnfound)
    {
        if (_count == 0)
        {
            var numElementsInOther = 0;
            foreach (var item in other)
            {
                numElementsInOther++;
                break;
            }

            return (UniqueCount: 0, UnfoundCount: numElementsInOther);
        }

        Debug.Assert(!_buckets.IsNullOrEmpty() && _count > 0, "_buckets was null but count greater than 0");

        var originalCount = _count;
        var intArrayLength = BitHelper.ToIntArrayLength(originalCount);

        int[] pooledArray = null;
        BitHelper bitHelper = new(intArrayLength <= StackAllocThreshold ?
                stackalloc int[intArrayLength] :
                new(pooledArray = _bucketPool.Rent(intArrayLength), 0, 100),
            true);

        var unfoundCount = 0;
        var uniqueFoundCount = 0;

        foreach (var item in other)
        {
            var index = FindItemIndex(item);
            if (index >= 0)
            {
                if (bitHelper.IsMarked(index)) continue;

                bitHelper.MarkBit(index);
                uniqueFoundCount++;
            }
            else
            {
                unfoundCount++;
                if (returnIfUnfound) break;
            }
        }

        if (pooledArray is not null) _bucketPool.Return(pooledArray);

        return (uniqueFoundCount, unfoundCount);
    }

    internal static bool EqualityComparersAreEqual(TempHashSet<T> set1, TempHashSet<T> set2)
        => set1.Comparer.Equals(set2.Comparer);

    internal static bool EqualityComparersAreEqual(TempHashSet<T> set1, HashSet<T> set2)
        => set1.Comparer.Equals(set2.Comparer);

    internal static bool EqualityComparersAreEqual(HashSet<T> set1, TempHashSet<T> set2)
        => set1.Comparer.Equals(set2.Comparer);

    void RenewBuckets(int newSize)
    {
        if (_buckets is not null) _bucketPool.Return(_buckets);

        var buckets = _bucketPool.Rent(newSize);
        Array.Clear(buckets, 0, buckets.Length);
        _buckets = buckets;
    }

    #endregion

    public ref struct Enumerator
    {
        readonly TempHashSet<T> _hashSet;
        readonly int _version;
        int _index;

        internal Enumerator(TempHashSet<T> hashSet)
        {
            _hashSet = hashSet;
            _version = hashSet._version;
            _index = 0;
            Current = default!;
        }

        public bool MoveNext()
        {
            if (_version != _hashSet._version)
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            while ((uint)_index < (uint)_hashSet._count)
            {
                ref var entry = ref _hashSet._entries![_index++];
                if (entry.Next < -1) continue;

                Current = entry.Value;
                return true;
            }

            _index = _hashSet._count + 1;
            Current = default!;
            return false;
        }

        public T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            private set;
        }
    }

    internal TempHashSet(T[] items) : this(items.AsSpan()) { }

    internal TempHashSet(T[] items, IEqualityComparer<T> comparer) : this(items.AsSpan(),
        comparer,
        ArrayPool<int>.Shared,
        ArrayPool<Entry<T>>.Shared) { }

    internal TempHashSet(T[] items,
        IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool) : this(items.AsSpan(), comparer, bucketPool, entryPool) { }

    internal TempHashSet(ReadOnlySpan<T> span, IEqualityComparer<T> comparer = null) : this(span,
        comparer,
        ArrayPool<int>.Shared,
        ArrayPool<Entry<T>>.Shared) { }

    internal TempHashSet(ReadOnlySpan<T> span,
        IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool) : this(comparer, bucketPool, entryPool)
    {
        Initialize(span.Length);
        UnionWith(span);

        if (_count > 0 && _entries!.Length / _count > ShrinkThreshold) TrimExcess();
    }

    internal readonly ref T FindValue(T equalValue)
    {
        if (_buckets.IsNullOrEmpty()) return ref Unsafe.NullRef<T>();

        var index = FindItemIndex(equalValue);
        return ref index >= 0 ? ref _entries![index].Value : ref Unsafe.NullRef<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnionWith(T[] other) => UnionWith((ReadOnlySpan<T>)other);

    public void UnionWith(ReadOnlySpan<T> other)
    {
        for (int i = 0, len = other.Length; i < len; i++) AddIfNotPresent(other[i], out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IntersectWith(T[] other) => IntersectWith((ReadOnlySpan<T>)other);

    public void IntersectWith(ReadOnlySpan<T> other)
    {
        if (_count == 0) return;

        if (other.Length == 0)
        {
            Clear();
            return;
        }

        IntersectWithSpan(other);
    }

    void IntersectWithSpan(ReadOnlySpan<T> other)
    {
        Debug.Assert(!_buckets.IsNullOrEmpty(), "_buckets shouldn't be null; callers should check first");

        var originalCount = _count;
        var intArrayLength = BitHelper.ToIntArrayLength(originalCount);

        int[] pooledArray = null;
        BitHelper bitHelper = new(intArrayLength <= StackAllocThreshold ?
                stackalloc int[intArrayLength] :
                new(pooledArray = _bucketPool.Rent(intArrayLength), 0, intArrayLength),
            true);

        for (int i = 0, len = other.Length; i < len; i++)
        {
            var index = FindItemIndex(other[i]);
            if (index >= 0) bitHelper.MarkBit(index);
        }

        for (var i = 0; i < originalCount; i++)
        {
            ref var entry = ref _entries![i];
            if (entry.Next >= -1 && !bitHelper.IsMarked(i)) Remove(entry.Value);
        }

        if (pooledArray is not null) _bucketPool.Return(pooledArray);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExceptWith(T[] other) => ExceptWith((ReadOnlySpan<T>)other);

    public void ExceptWith(ReadOnlySpan<T> other)
    {
        if (_count == 0) return;

        for (int i = 0, len = other.Length; i < len; i++) Remove(other[i]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SymmetricExceptWith(T[] other) => SymmetricExceptWith((ReadOnlySpan<T>)other);

    public void SymmetricExceptWith(ReadOnlySpan<T> other)
    {
        if (_count == 0)
        {
            UnionWith(other);
            return;
        }

        SymmetricExceptWithSpan(other);
    }

    void SymmetricExceptWithSpan(ReadOnlySpan<T> other)
    {
        var originalCount = _count;
        var intArrayLength = BitHelper.ToIntArrayLength(originalCount);

        int[] itemsToRemoveArray = null;
        BitHelper itemsToRemove = new(intArrayLength <= StackAllocThreshold / 2 ?
                stackalloc int[intArrayLength] :
                new(itemsToRemoveArray = _bucketPool.Rent(intArrayLength), 0, intArrayLength),
            true);

        int[] itemsAddedFromOtherArray = null;
        BitHelper itemsAddedFromOther = new(itemsToRemoveArray is null ?
                stackalloc int[intArrayLength] :
                new(itemsAddedFromOtherArray = _bucketPool.Rent(intArrayLength), 0, intArrayLength),
            true);

        for (int i = 0, len = other.Length; i < len; i++)
            if (AddIfNotPresent(other[i], out var location)) itemsAddedFromOther.MarkBit(location);
            else
            {
                if (location < originalCount && !itemsAddedFromOther.IsMarked(location))
                    itemsToRemove.MarkBit(location);
            }

        for (var i = 0; i < originalCount; i++)
            if (itemsToRemove.IsMarked(i))
                Remove(_entries![i].Value);

        if (itemsToRemoveArray is null) return;

        _bucketPool.Return(itemsToRemoveArray);
        _bucketPool.Return(itemsAddedFromOtherArray);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSubsetOf(T[] other) => IsSubsetOf((ReadOnlySpan<T>)other);

    public bool IsSubsetOf(ReadOnlySpan<T> other)
    {
        if (_count == 0) return true;

        var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, false);
        return uniqueCount == Count && unfoundCount >= 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsProperSubsetOf(T[] other) => IsProperSubsetOf((ReadOnlySpan<T>)other);

    public bool IsProperSubsetOf(ReadOnlySpan<T> other)
    {
        if (other.Length == 0) return false;

        if (_count == 0) return other.Length > 0;

        var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, false);
        return uniqueCount == Count && unfoundCount > 0;
    }

    (int UniqueCount, int UnfoundCount) CheckUniqueAndUnfoundElements(ReadOnlySpan<T> other, bool returnIfUnfound)
    {
        if (_count == 0)
        {
            var numElementsInOther = 0;
            foreach (var _ in other)
            {
                numElementsInOther++;
                break;
            }

            return (UniqueCount: 0, UnfoundCount: numElementsInOther);
        }

        Debug.Assert(!_buckets.IsNullOrEmpty() && _count > 0, "_buckets was null but count greater than 0");

        var originalCount = _count;
        var intArrayLength = BitHelper.ToIntArrayLength(originalCount);

        int[] pooledArray = null;
        BitHelper bitHelper = new(intArrayLength <= StackAllocThreshold ?
                stackalloc int[intArrayLength] :
                new(pooledArray = _bucketPool.Rent(intArrayLength), 0, intArrayLength),
            true);

        var unfoundCount = 0;
        var uniqueFoundCount = 0;

        for (int i = 0, len = other.Length; i < len; i++)
        {
            var index = FindItemIndex(other[i]);
            if (index >= 0)
            {
                if (bitHelper.IsMarked(index)) continue;

                bitHelper.MarkBit(index);
                uniqueFoundCount++;
            }
            else
            {
                unfoundCount++;
                if (returnIfUnfound) break;
            }
        }

        if (pooledArray is not null) _bucketPool.Return(pooledArray);

        return (uniqueFoundCount, unfoundCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSupersetOf(T[] other) => IsSupersetOf((ReadOnlySpan<T>)other);

    public bool IsSupersetOf(ReadOnlySpan<T> other) => other.Length == 0 ? true : ContainsAllElements(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsProperSupersetOf(T[] other) => IsProperSupersetOf((ReadOnlySpan<T>)other);

    public bool IsProperSupersetOf(ReadOnlySpan<T> other)
    {
        if (_count == 0) return false;

        if (other.Length == 0) return true;

        var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, false);
        return uniqueCount < Count && unfoundCount == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Overlaps(T[] other) => Overlaps((ReadOnlySpan<T>)other);

    public bool Overlaps(ReadOnlySpan<T> other)
    {
        if (_count == 0) return false;

        for (int i = 0, len = other.Length; i < len; i++)
            if (Contains(other[i]))
                return true;

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetEquals(T[] other) => SetEquals((ReadOnlySpan<T>)other);

    public bool SetEquals(ReadOnlySpan<T> other)
    {
        if (_count == 0 && other.Length > 0) return false;

        var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, true);
        return uniqueCount == Count && unfoundCount == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped Span<T> span) => CopyTo(span, 0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped Span<T> dest, int destIndex) => CopyTo(dest, destIndex, Count);

    public void CopyTo(scoped Span<T> dest, int destIndex, int count)
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

            dest[destIndex++] = entry.Value;
            count--;
        }
    }

    bool ContainsAllElements(ReadOnlySpan<T> other)
    {
        for (int i = 0, len = other.Length; i < len; i++)
            if (!Contains(other[i]))
                return false;

        return true;
    }

    void ReturnBuckets(int[] replaceWith)
    {
        if (_buckets is not null) _bucketPool.Return(_buckets);

        _buckets = replaceWith ?? s_emptyBuckets;
    }

    void ReturnEntries(Entry<T>[] replaceWith)
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