// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/HashSet.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic.Value;

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

public partial struct ValueHashSet<T> : ISet<T>, IReadOnlySet<T>
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

    internal ValueHashSet(IEqualityComparer<T> comparer, ArrayPool<int> bucketPool, ArrayPool<Entry<T>> entryPool)
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

    internal ValueHashSet(IEnumerable<T> collection,
        IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool) : this(comparer, bucketPool, entryPool)
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (collection is ValueHashSet<T> otherAsSet && EqualityComparersAreEqual(this, otherAsSet))
            ConstructFrom(otherAsSet);
        else
        {
            if (collection is ICollection<T> coll)
            {
                var count = coll.Count;
                if (count > 0) Initialize(count);
            }

            UnionWith(collection);

            if (_count > 0 && _entries!.Length / _count > ShrinkThreshold) TrimExcess();
        }
    }

    internal ValueHashSet(int capacity,
        IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool) : this(comparer, bucketPool, entryPool)
    {
        if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);

        if (capacity > 0) Initialize(capacity);
    }

    void ConstructFrom(ValueHashSet<T> source)
    {
        if (source.Count == 0) return;

        var capacity = source._buckets!.Length;
        var threshold = HashHelpers.ExpandPrime(source.Count + 1);

        if (threshold >= capacity)
        {
            _buckets = (int[])source._buckets.Clone();
            _entries = (Entry<T>[])source._entries!.Clone();
            _freeList = source._freeList;
            _freeCount = source._freeCount;
            _count = source._count;
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
            _fastModMultiplier = source._fastModMultiplier;
#endif
        }
        else
        {
            Initialize(source.Count);

            var entries = source.GetEntries();
            for (int i = 0, len = entries.Length; i < len; i++)
            {
                ref var entry = ref entries![i];
                if (entry.Next >= -1) AddIfNotPresent(entry.Value, out _);
            }
        }
    }

    #endregion

    #region ICollection<T> methods

    void ICollection<T>.Add(T item) => AddIfNotPresent(item, out _);

    public void Clear()
    {
        var count = _count;
        if (count <= 0) return;

        Array.Clear(_buckets, 0, _buckets.Length);
        _count = 0;
        _freeList = -1;
        _freeCount = 0;
        Array.Clear(_entries, 0, count);
    }

    public bool Contains(T item) => FindItemIndex(item) >= 0;

    int FindItemIndex(T item)
    {
        var buckets = _buckets;
        if (buckets is null) return -1;

        var entries = _entries;

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

                    if (++collisionCount > (uint)entries.Length)
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

                    if (++collisionCount > (uint)entries.Length)
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

                if (++collisionCount > (uint)entries.Length)
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
            }
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ref int GetBucketRef(int hashCode)
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

                entry.Next = StartOfFreeList - _freeList;

                if (s_clearEntries) entry.Value = default!;

                _freeList = i;
                _freeCount++;
                return true;
            }

            last = i;
            i = entry.Next;

            if (++collisionCount > (uint)entries.Length)
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

    bool ICollection<T>.IsReadOnly => false;

    #endregion

    #region IEnumerable methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(in this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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

        switch (other)
        {
            case ValueHashSet<T> otherSet when otherSet._buckets == _buckets: return;

            case ICollection<T> { Count: 0 }:
                Clear();
                return;

            case ValueHashSet<T> otherAsSet when EqualityComparersAreEqual(this, otherAsSet):
                IntersectWithHashSetWithSameComparer(otherAsSet);
                return;

            case HashSet<T> otherAsSCGSet when EqualityComparersAreEqual(this, otherAsSCGSet):
                IntersectWithHashSetWithSameComparer(otherAsSCGSet);
                return;

            default: IntersectWithEnumerable(other); break;
        }
    }

    public void ExceptWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0) return;

        if (other is ValueHashSet<T> otherSet && otherSet._buckets == _buckets)
        {
            Clear();
            return;
        }

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

        switch (other)
        {
            case ValueHashSet<T> otherSet when otherSet._buckets == _buckets:
                Clear();
                return;

            case ValueHashSet<T> otherAsSet when EqualityComparersAreEqual(this, otherAsSet):
                SymmetricExceptWithUniqueHashSet(otherAsSet);
                break;

            case HashSet<T> otherAsSCGSet when EqualityComparersAreEqual(this, otherAsSCGSet):
                SymmetricExceptWithUniqueHashSet(otherAsSCGSet);
                break;

            default: SymmetricExceptWithEnumerable(other); break;
        }
    }

    public bool IsSubsetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0) return true;

        switch (other)
        {
            case ValueHashSet<T> otherSet when otherSet._buckets == _buckets: return true;

            case ValueHashSet<T> otherAsSet when EqualityComparersAreEqual(this, otherAsSet):
                return Count <= otherAsSet.Count && IsSubsetOfHashSetWithSameComparer(otherAsSet);

            case HashSet<T> otherAsSCGSet when EqualityComparersAreEqual(this, otherAsSCGSet):
                return Count <= otherAsSCGSet.Count && IsSubsetOfHashSetWithSameComparer(otherAsSCGSet);

            default:
                var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, false);
                return uniqueCount == Count && unfoundCount >= 0;
        }
    }

    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        switch (other)
        {
            case ValueHashSet<T> otherSet when otherSet._buckets == _buckets:
            case ICollection<T> { Count: 0 }:
                return false;

            case ICollection<T> otherAsCollection when Count == 0: return otherAsCollection.Count > 0;

            case ValueHashSet<T> otherAsSet when EqualityComparersAreEqual(this, otherAsSet):
                return Count < otherAsSet.Count && IsSubsetOfHashSetWithSameComparer(otherAsSet);

            case HashSet<T> otherAsSCGSet when EqualityComparersAreEqual(this, otherAsSCGSet):
                return Count < otherAsSCGSet.Count && IsSubsetOfHashSetWithSameComparer(otherAsSCGSet);

            default:
                var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, false);
                return uniqueCount == Count && unfoundCount > 0;
        }
    }

    public bool IsSupersetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        switch (other)
        {
            case ValueHashSet<T> otherSet when otherSet._buckets == _buckets:
            case ICollection<T> { Count: 0 }:
                return true;

            case ValueHashSet<T> otherAsSet
                when EqualityComparersAreEqual(this, otherAsSet) && otherAsSet.Count > Count:

            case HashSet<T> otherAsSCGSet
                when EqualityComparersAreEqual(this, otherAsSCGSet) && otherAsSCGSet.Count > Count:
                return false;

            default: return ContainsAllElements(other);
        }
    }

    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0) return false;

        switch (other)
        {
            case ValueHashSet<T> otherSet when otherSet._buckets == _buckets: return false;
            case ICollection<T> { Count: 0 }: return true;

            case ValueHashSet<T> otherAsSet when EqualityComparersAreEqual(this, otherAsSet):
                return otherAsSet.Count < Count && ContainsAllElements(otherAsSet);

            case HashSet<T> otherAsSCGSet when EqualityComparersAreEqual(this, otherAsSCGSet):
                return otherAsSCGSet.Count < Count && ContainsAllElements(otherAsSCGSet);

            default:
                var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, true);
                return uniqueCount < Count && unfoundCount == 0;
        }
    }

    public bool Overlaps(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0) return false;

        if (other is ValueHashSet<T> otherSet && otherSet._buckets == _buckets) return true;

        foreach (var element in other)
            if (Contains(element))
                return true;

        return false;
    }

    public bool SetEquals(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        switch (other)
        {
            case ValueHashSet<T> otherSet when otherSet._buckets == _buckets: return true;

            case ValueHashSet<T> otherAsSet when EqualityComparersAreEqual(this, otherAsSet):
                return Count == otherAsSet.Count && ContainsAllElements(otherAsSet);

            case HashSet<T> otherAsSCGSet when EqualityComparersAreEqual(this, otherAsSCGSet):
                return Count == otherAsSCGSet.Count && ContainsAllElements(otherAsSCGSet);

            case ICollection<T> { Count: > 0 } when Count == 0: return false;

            default:
                var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, true);
                return uniqueCount == Count && unfoundCount == 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest) => CopyTo(dest, 0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] array, int arrayIndex) => CopyTo(array, arrayIndex, Count);

    public void CopyTo(T[] dest, int destIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(dest);

        var span = dest.AsSpan();
        CopyTo(in span, destIndex, count);
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
            if (entry.Next >= -1)
            {
                ref var bucket = ref GetBucketRef(entry.HashCode);
                entry.Next = bucket - 1;
                bucket = i + 1;
            }
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
            if (oldEntries[i].Next >= -1)
            {
                ref var entry = ref entries![count];
                entry = oldEntries[i];
                ref var bucket = ref GetBucketRef(hashCode);
                entry.Next = bucket - 1;
                bucket = count + 1;
                count++;
            }
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

        var entries = _entries;

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

        if (!typeof(T).IsValueType && collisionCount > HashHelpers.HashCollisionThreshold &&
            comparer.IsNonRandomizedStringComparer())
        {
            Resize(entries.Length, true);
            location = FindItemIndex(value);
        }

        return true;
    }

    bool ContainsAllElements(IEnumerable<T> other)
    {
        foreach (var element in other)
            if (!Contains(element))
                return false;

        return true;
    }

    internal bool IsSubsetOfHashSetWithSameComparer(ValueHashSet<T> other)
    {
        var entries = _entries;
        for (var i = 0; i < _count; i++)
        {
            ref var entry = ref entries![i];
            if (entry.Next >= -1)
            {
                var item = entry.Value;
                if (!other.Contains(item)) return false;
            }
        }

        return true;
    }

    internal bool IsSubsetOfHashSetWithSameComparer(HashSet<T> other)
    {
        var entries = _entries;
        for (var i = 0; i < _count; i++)
        {
            ref var entry = ref entries![i];
            if (entry.Next >= -1)
            {
                var item = entry.Value;
                if (!other.Contains(item)) return false;
            }
        }

        return true;
    }

    internal void IntersectWithHashSetWithSameComparer(ValueHashSet<T> other)
    {
        var entries = _entries;
        for (var i = 0; i < _count; i++)
        {
            ref var entry = ref entries![i];
            if (entry.Next >= -1)
            {
                var item = entry.Value;
                if (!other.Contains(item)) Remove(item);
            }
        }
    }

    internal void IntersectWithHashSetWithSameComparer(HashSet<T> other)
    {
        var entries = _entries;
        for (var i = 0; i < _count; i++)
        {
            ref var entry = ref entries![i];
            if (entry.Next >= -1)
            {
                var item = entry.Value;
                if (!other.Contains(item)) Remove(item);
            }
        }
    }

    void IntersectWithEnumerable(IEnumerable<T> other)
    {
        var originalCount = _count;
        var intArrayLength = BitHelper.ToIntArrayLength(originalCount);

        Span<int> span = stackalloc int[StackAllocThreshold];
        var bitHelper = intArrayLength <= StackAllocThreshold ?
            new(span.Slice(0, intArrayLength), true) :
            new BitHelper(new int[intArrayLength], false);

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
    }

    void SymmetricExceptWithUniqueHashSet(ValueHashSet<T> other)
    {
        var otherEntries = other.GetEntries();

        for (int i = 0, len = otherEntries.Length; i < len; i++)
        {
            ref var entry = ref otherEntries![i];
            if (entry.Next >= -1)
            {
                var item = entry.Value;
                if (!Remove(item)) AddIfNotPresent(item, out _);
            }
        }
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

        Span<int> itemsToRemoveSpan = stackalloc int[StackAllocThreshold / 2];
        var itemsToRemove = intArrayLength <= StackAllocThreshold / 2 ?
            new(itemsToRemoveSpan.Slice(0, intArrayLength), true) :
            new BitHelper(new int[intArrayLength], false);

        Span<int> itemsAddedFromOtherSpan = stackalloc int[StackAllocThreshold / 2];
        var itemsAddedFromOther = intArrayLength <= StackAllocThreshold / 2 ?
            new(itemsAddedFromOtherSpan.Slice(0, intArrayLength), true) :
            new BitHelper(new int[intArrayLength], false);

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

        var originalCount = _count;
        var intArrayLength = BitHelper.ToIntArrayLength(originalCount);

        Span<int> span = stackalloc int[StackAllocThreshold];
        var bitHelper = intArrayLength <= StackAllocThreshold ?
            new(span.Slice(0, intArrayLength), true) :
            new BitHelper(new int[intArrayLength], false);

        var unfoundCount = 0;
        var uniqueFoundCount = 0;

        foreach (var item in other)
        {
            var index = FindItemIndex(item);
            if (index >= 0)
            {
                if (!bitHelper.IsMarked(index))
                {
                    bitHelper.MarkBit(index);
                    uniqueFoundCount++;
                }
            }
            else
            {
                unfoundCount++;
                if (returnIfUnfound) break;
            }
        }

        return (uniqueFoundCount, unfoundCount);
    }

    internal static bool EqualityComparersAreEqual(ValueHashSet<T> set1, ValueHashSet<T> set2)
        => set1.Comparer.Equals(set2.Comparer);

    internal static bool EqualityComparersAreEqual(ValueHashSet<T> set1, HashSet<T> set2)
        => set1.Comparer.Equals(set2.Comparer);

    internal static bool EqualityComparersAreEqual(HashSet<T> set1, ValueHashSet<T> set2)
        => set1.Comparer.Equals(set2.Comparer);

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

    void RenewEntries(int newSize)
    {
        if (_entries is not null)
            try
            {
                _entryPool.Return(_entries, s_clearEntries);
            }
            catch
            {
                if (s_clearEntries) Array.Clear(_entries, 0, _entries.Length);
            }

        _entries = _entryPool.Rent(newSize);
    }

    #endregion

    public struct Enumerator : IEnumerator<T>
    {
        readonly ValueHashSet<T> _hashSet;
        readonly int _version;
        int _index;

        public Enumerator(scoped ref readonly ValueHashSet<T> hashSet)
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
                if (entry.Next >= -1)
                {
                    Current = entry.Value;
                    return true;
                }
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

        public readonly void Dispose() { }

        readonly object IEnumerator.Current
        {
            get
            {
                if (_index == 0 || _index == _hashSet._count + 1)
                    ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();

                return Current;
            }
        }

        void IEnumerator.Reset()
        {
            if (_version != _hashSet._version)
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            _index = 0;
            Current = default!;
        }
    }
}
