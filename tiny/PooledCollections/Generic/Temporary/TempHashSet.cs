// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/HashSet.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Buffers;
using System.Collections;
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

public ref struct TempHashSet<T>
{
    // This uses the same array-based implementation as Dictionary<TKey, TValue>.

    /// <summary> Cutoff point for stackallocs. This corresponds to the number of ints. </summary>
    const int StackAllocThreshold = 100;

    /// <summary>
    ///     When constructing a hashset from an existing collection, it may contain duplicates, so this is used as the max
    ///     acceptable excess ratio of capacity to count. Note that this is only used on the ctor and not to automatically shrink if
    ///     the hashset has, e.g, a lot of adds followed by removes. Users must explicitly shrink by calling TrimExcess. This is set
    ///     to 3 because capacity is acceptable as 2x rounded up to nearest prime.
    /// </summary>
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

    internal static readonly IEqualityComparer<string> _stringComparer = PooledDictionary<string, byte>._stringComparer;

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

        if (comparer is not null &&
            comparer !=
            EqualityComparer<T>
                .Default) // first check for null to avoid forcing default comparer instantiation unnecessarily
            _comparer = comparer;

        // Special-case EqualityComparer<string>.Default, StringComparer.Ordinal, and StringComparer.OrdinalIgnoreCase.
        // We use a non-randomized comparer for improved perf, falling back to a randomized comparer if the
        // hash buckets become unbalanced.
        if (typeof(T) == typeof(string)) _comparer = (IEqualityComparer<T>)_stringComparer;
    }

    internal TempHashSet(IEnumerable<T> collection,
        IEqualityComparer<T> comparer,
        ArrayPool<int> bucketPool,
        ArrayPool<Entry<T>> entryPool) : this(comparer, bucketPool, entryPool)
    {
        ArgumentNullException.ThrowIfNull(collection);

        // To avoid excess resizes, first set size based on collection's count. The collection may
        // contain duplicates, so call TrimExcess if resulting HashSet is larger than the threshold.
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

    /// <summary> Initializes the HashSet from another HashSet with the same element type and equality comparer. </summary>
    void ConstructFrom(TempHashSet<T> source)
    {
        if (source.Count == 0)

            // As well as short-circuiting on the rest of the work done,
            // this avoids errors from trying to access source._buckets
            // or source._entries when they aren't initialized.
            return;

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

        Debug.Assert(Count == source.Count);
    }

    #endregion

    #region ICollection<T> methods

    /// <summary> Removes all elements from the <see cref="TempHashSet{T}"/> object. </summary>
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

    /// <summary> Determines whether the <see cref="TempHashSet{T}"/> contains the specified element. </summary>
    /// <param name="item"> The element to locate in the <see cref="TempHashSet{T}"/> object. </param>
    /// <returns> true if the <see cref="TempHashSet{T}"/> object contains the specified element; otherwise, false. </returns>
    public bool Contains(T item) => FindItemIndex(item) >= 0;

    /// <summary> Gets the index of the item in <see cref="_entries"/>, or -1 if it's not in the set. </summary>
    readonly int FindItemIndex(T item)
    {
        var buckets = _buckets;
        if (buckets is not null)
        {
            var entries = _entries;
            Debug.Assert(entries is not null, "Expected _entries to be initialized");

            uint collisionCount = 0;
            var comparer = _comparer;

            if (comparer is null)
            {
                var hashCode = item is not null ? item.GetHashCode() : 0;
                if (typeof(T).IsValueType)
                {
                    // ValueType: Devirtualize with EqualityComparer<TValue>.Default intrinsic
                    var i = GetBucketRef(hashCode) - 1; // Value in _buckets is 1-based
                    while (i >= 0)
                    {
                        ref var entry = ref entries[i];
                        if (entry.HashCode == hashCode && EqualityComparer<T>.Default.Equals(entry.Value, item)) return i;

                        i = entry.Next;

                        collisionCount++;
                        if (collisionCount > (uint)entries.Length)

                            // The chain of entries forms a loop, which means a concurrent update has happened.
                            ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                    }
                }
                else
                {
                    // Object type: Shared Generic, EqualityComparer<TValue>.Default won't devirtualize (https://github.com/dotnet/runtime/issues/10050),
                    // so cache in a local rather than get EqualityComparer per loop iteration.
                    var defaultComparer = EqualityComparer<T>.Default;
                    var i = GetBucketRef(hashCode) - 1; // Value in _buckets is 1-based
                    while (i >= 0)
                    {
                        ref var entry = ref entries[i];
                        if (entry.HashCode == hashCode && defaultComparer.Equals(entry.Value, item)) return i;

                        i = entry.Next;

                        collisionCount++;
                        if (collisionCount > (uint)entries.Length)

                            // The chain of entries forms a loop, which means a concurrent update has happened.
                            ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                    }
                }
            }
            else
            {
                var hashCode = item is not null ? comparer.GetHashCode(item) : 0;
                var i = GetBucketRef(hashCode) - 1; // Value in _buckets is 1-based
                while (i >= 0)
                {
                    ref var entry = ref entries[i];
                    if (entry.HashCode == hashCode && comparer.Equals(entry.Value, item)) return i;

                    i = entry.Next;

                    collisionCount++;
                    if (collisionCount > (uint)entries.Length)

                        // The chain of entries forms a loop, which means a concurrent update has happened.
                        ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }
            }
        }

        return -1;
    }

    /// <summary> Gets a reference to the specified hashcode's bucket, containing an index into <see cref="_entries"/>. </summary>
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
        if (_buckets is not null)
        {
            var entries = _entries;
            Debug.Assert(entries is not null, "entries should be non-null");

            uint collisionCount = 0;
            var last = -1;
            var hashCode = item is not null ? _comparer?.GetHashCode(item) ?? item.GetHashCode() : 0;

            ref var bucket = ref GetBucketRef(hashCode);
            var i = bucket - 1; // Value in buckets is 1-based

            while (i >= 0)
            {
                ref var entry = ref entries[i];

                if (entry.HashCode == hashCode &&
                    (_comparer?.Equals(entry.Value, item) ?? EqualityComparer<T>.Default.Equals(entry.Value, item)))
                {
                    if (last < 0) bucket = entry.Next + 1; // Value in buckets is 1-based
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

                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
            }
        }

        return false;
    }

    /// <summary> Gets the number of elements that are contained in the set. </summary>
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

    /// <summary> Adds the specified element to the <see cref="TempHashSet{T}"/>. </summary>
    /// <param name="item"> The element to add to the set. </param>
    /// <returns> true if the element is added to the <see cref="TempHashSet{T}"/> object; false if the element is already present. </returns>
    public bool Add(T item) => AddIfNotPresent(item, out _);

    /// <summary> Searches the set for a given value and returns the equal value it finds, if any. </summary>
    /// <param name="equalValue"> The value to search for. </param>
    /// <param name="actualValue">
    ///     The value from the set that the search found, or the default value of <typeparamref name="T"/>
    ///     when the search yielded no match.
    /// </param>
    /// <returns> A value indicating whether the search was successful. </returns>
    /// <remarks>
    ///     This can be useful when you want to reuse a previously stored reference instead of a newly constructed one (so
    ///     that more sharing of references can occur) or to look up a value that has more complete data than the value you
    ///     currently have, although their comparer functions indicate they are equal.
    /// </remarks>
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

    /// <summary>
    ///     Modifies the current <see cref="TempHashSet{T}"/> object to contain all elements that are present in itself, the
    ///     specified collection, or both.
    /// </summary>
    /// <param name="other"> The collection to compare to the current <see cref="TempHashSet{T}"/> object. </param>
    public void UnionWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var item in other) AddIfNotPresent(item, out _);
    }

    /// <summary>
    ///     Modifies the current <see cref="TempHashSet{T}"/> object to contain only elements that are present in that object
    ///     and in the specified collection.
    /// </summary>
    /// <param name="other"> The collection to compare to the current <see cref="TempHashSet{T}"/> object. </param>
    public void IntersectWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // Intersection of anything with empty set is empty set, so return if count is 0.
        // Same if the set intersecting with itself is the same set.
        if (Count == 0) return;

        // If other is known to be empty, intersection is empty set; remove all elements, and we're done.
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

    /// <summary> Removes all elements in the specified collection from the current <see cref="TempHashSet{T}"/> object. </summary>
    /// <param name="other"> The collection to compare to the current <see cref="TempHashSet{T}"/> object. </param>
    public void ExceptWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // This is already the empty set; return.
        if (Count == 0) return;

        // Remove every element in other from this.
        foreach (var element in other) Remove(element);
    }

    /// <summary>
    ///     Modifies the current <see cref="TempHashSet{T}"/> object to contain only elements that are present either in that
    ///     object or in the specified collection, but not both.
    /// </summary>
    /// <param name="other"> The collection to compare to the current <see cref="TempHashSet{T}"/> object. </param>
    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // If set is empty, then symmetric difference is other.
        if (Count == 0)
        {
            UnionWith(other);
            return;
        }

        // If other is a HashSet, it has unique elements according to its equality comparer,
        // but if they're using different equality comparers, then assumption of uniqueness
        // will fail. So first check if other is a hashset using the same equality comparer;
        // symmetric except is a lot faster and avoids bit array allocations if we can assume
        // uniqueness.
        if (other is HashSet<T> otherAsSCGSet && EqualityComparersAreEqual(this, otherAsSCGSet))
            SymmetricExceptWithUniqueHashSet(otherAsSCGSet);
        else SymmetricExceptWithEnumerable(other);
    }

    /// <summary> Determines whether a <see cref="TempHashSet{T}"/> object is a subset of the specified collection. </summary>
    /// <param name="other"> The collection to compare to the current <see cref="TempHashSet{T}"/> object. </param>
    /// <returns> true if the <see cref="TempHashSet{T}"/> object is a subset of <paramref name="other"/>; otherwise, false. </returns>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // The empty set is a subset of any set, and a set is a subset of itself.
        // Set is always a subset of itself
        if (Count == 0) return true;

        // Faster if other has unique elements according to this equality comparer; so check
        // that other is a hashset using the same equality comparer.
        if (other is HashSet<T> otherAsSCGSet && EqualityComparersAreEqual(this, otherAsSCGSet))
        {
            // if this has more elements then it can't be a subset
            if (Count > otherAsSCGSet.Count) return false;

            // already checked that we're using same equality comparer. simply check that
            // each element in this is contained in other.
            return IsSubsetOfHashSetWithSameComparer(otherAsSCGSet);
        }

        var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, false);
        return uniqueCount == Count && unfoundCount >= 0;
    }

    /// <summary> Determines whether a <see cref="TempHashSet{T}"/> object is a proper subset of the specified collection. </summary>
    /// <param name="other"> The collection to compare to the current <see cref="TempHashSet{T}"/> object. </param>
    /// <returns> true if the <see cref="TempHashSet{T}"/> object is a proper subset of <paramref name="other"/>; otherwise, false. </returns>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (other is ICollection<T> otherAsCollection)
        {
            // No set is a proper subset of an empty set.
            if (otherAsCollection.Count == 0) return false;

            // The empty set is a proper subset of anything but the empty set.
            if (Count == 0) return otherAsCollection.Count > 0;

            // Faster if other is a hashset (and we're using same equality comparer).
            if (other is HashSet<T> otherAsSCGSet && EqualityComparersAreEqual(this, otherAsSCGSet))
            {
                if (Count >= otherAsSCGSet.Count) return false;

                // This has strictly less than number of items in other, so the following
                // check suffices for proper subset.
                return IsSubsetOfHashSetWithSameComparer(otherAsSCGSet);
            }
        }

        var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, false);
        return uniqueCount == Count && unfoundCount > 0;
    }

    /// <summary> Determines whether a <see cref="TempHashSet{T}"/> object is a proper superset of the specified collection. </summary>
    /// <param name="other"> The collection to compare to the current <see cref="TempHashSet{T}"/> object. </param>
    /// <returns> true if the <see cref="TempHashSet{T}"/> object is a superset of <paramref name="other"/>; otherwise, false. </returns>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // Try to fall out early based on counts.
        if (other is ICollection<T> otherAsCollection)
        {
            // If other is the empty set then this is a superset.
            if (otherAsCollection.Count == 0) return true;

            // Try to compare based on counts alone if other is a hashset with same equality comparer.
            if (other is HashSet<T> otherAsSCGSet &&
                EqualityComparersAreEqual(this, otherAsSCGSet) &&
                otherAsSCGSet.Count > Count) return false;
        }

        return ContainsAllElements(other);
    }

    /// <summary> Determines whether a <see cref="TempHashSet{T}"/> object is a proper superset of the specified collection. </summary>
    /// <param name="other"> The collection to compare to the current <see cref="TempHashSet{T}"/> object. </param>
    /// <returns>
    ///     true if the <see cref="TempHashSet{T}"/> object is a proper superset of <paramref name="other"/>; otherwise,
    ///     false.
    /// </returns>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // The empty set isn't a proper superset of any set, and a set is never a strict superset of itself.
        if (Count == 0) return false;

        if (other is ICollection<T> otherAsCollection)
        {
            // If other is the empty set then this is a superset.
            if (otherAsCollection.Count == 0)

                // Note that this has at least one element, based on above check.
                return true;

            // Faster if other is a hashset with the same equality comparer
            if (other is HashSet<T> otherAsSCGSet && EqualityComparersAreEqual(this, otherAsSCGSet))
            {
                if (otherAsSCGSet.Count >= Count) return false;

                // Now perform element check.
                return ContainsAllElements(otherAsSCGSet);
            }
        }

        // Couldn't fall out in the above cases; do it the long way
        var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, true);
        return uniqueCount < Count && unfoundCount == 0;
    }

    /// <summary>
    ///     Determines whether the current <see cref="TempHashSet{T}"/> object and a specified collection share common
    ///     elements.
    /// </summary>
    /// <param name="other"> The collection to compare to the current <see cref="TempHashSet{T}"/> object. </param>
    /// <returns>
    ///     true if the <see cref="TempHashSet{T}"/> object and <paramref name="other"/> share at least one common element;
    ///     otherwise, false.
    /// </returns>
    public bool Overlaps(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Count == 0) return false;

        foreach (var element in other)
            if (Contains(element))
                return true;

        return false;
    }

    /// <summary> Determines whether a <see cref="TempHashSet{T}"/> object and the specified collection contain the same elements. </summary>
    /// <param name="other"> The collection to compare to the current <see cref="TempHashSet{T}"/> object. </param>
    /// <returns> true if the <see cref="TempHashSet{T}"/> object is equal to <paramref name="other"/>; otherwise, false. </returns>
    public bool SetEquals(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (other is HashSet<T> otherAsSCGSet && EqualityComparersAreEqual(this, otherAsSCGSet))
        {
            // Attempt to return early: since both contain unique elements, if they have
            // different counts, then they can't be equal.
            if (Count != otherAsSCGSet.Count) return false;

            // Already confirmed that the sets have the same number of distinct elements, so if
            // one is a superset of the other then they must be equal.
            return ContainsAllElements(otherAsSCGSet);
        }

        // If this count is 0 but other contains at least one element, they can't be equal.
        if (Count == 0 && other is ICollection<T> otherAsCollection && otherAsCollection.Count > 0) return false;

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

    /// <summary>
    ///     Removes all elements that match the conditions defined by the specified predicate from a
    ///     <see cref="TempHashSet{T}"/> collection.
    /// </summary>
    public int RemoveWhere(Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var entries = _entries;
        var numRemoved = 0;
        for (var i = 0; i < _count; i++)
        {
            ref var entry = ref entries![i];
            if (entry.Next >= -1)
            {
                // Cache value in case delegate removes it
                var value = entry.Value;
                if (match(value))

                    // Check again that remove actually removed it.
                    if (Remove(value))
                        numRemoved++;
            }
        }

        return numRemoved;
    }

    /// <summary> Gets the <see cref="IEqualityComparer"/> object that is used to determine equality for the values in the set. </summary>
    public IEqualityComparer<T> Comparer => _comparer ?? EqualityComparer<T>.Default;

    /// <summary> Ensures that this hash set can hold the specified number of elements without growing. </summary>
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
        // Value types never rehash
        Debug.Assert(!forceNewHashCodes || !typeof(T).IsValueType);
        Debug.Assert(_entries is not null, "_entries should be non-null");
        Debug.Assert(newSize >= _entries.Length);

        var count = _count;
        var entries = _entryPool.Rent(newSize);
        Array.Copy(_entries, entries, count);

        if (!typeof(T).IsValueType && forceNewHashCodes)
        {
            _comparer = EqualityComparer<T>.Default;

            for (var i = 0; i < count; i++)
            {
                ref var entry = ref entries[i];
                if (entry.Next >= -1) entry.HashCode = entry.Value is not null ? _comparer!.GetHashCode(entry.Value) : 0;
            }

            if (ReferenceEquals(_comparer, EqualityComparer<T>.Default)) _comparer = null;
        }

        // Assign member variables after both arrays allocated to guard against corruption from OOM if second fails
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
                entry.Next = bucket - 1; // Value in _buckets is 1-based
                bucket = i + 1;
            }
        }

        _entryPool.Return(_entries, s_clearEntries);
        _entries = entries;
    }

    /// <summary>
    ///     Sets the capacity of a <see cref="TempHashSet{T}"/> object to the actual number of elements it contains, rounded
    ///     up to a nearby, implementation-specific value.
    /// </summary>
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
            var hashCode = oldEntries![i].HashCode; // At this point, we know we have entries.
            if (oldEntries[i].Next >= -1)
            {
                ref var entry = ref entries![count];
                entry = oldEntries[i];
                ref var bucket = ref GetBucketRef(hashCode);
                entry.Next = bucket - 1; // Value in _buckets is 1-based
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

    /// <summary>
    ///     Initializes buckets and slots arrays. Uses suggested capacity by finding next prime greater than or equal to
    ///     capacity.
    /// </summary>
    int Initialize(int capacity)
    {
        var size = HashHelpers.GetPrime(capacity);

        var buckets = _bucketPool.Rent(size);
        Array.Clear(buckets, 0, buckets.Length);
        _buckets = buckets;

        _entries = _entryPool.Rent(size);

        // Assign member variables after both arrays are allocated to guard against corruption from OOM if second fails.
        _freeList = -1;

#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)size);
#endif

        return size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Entry<T>[] GetEntries() => _entries ?? s_emptyEntries;

    /// <summary> Adds the specified element to the set if it's not already contained. </summary>
    /// <param name="value"> The element to add to the set. </param>
    /// <param name="location"> The index into <see cref="_entries"/> of the element. </param>
    /// <returns> true if the element is added to the <see cref="TempHashSet{T}"/> object; false if the element is already present. </returns>
    internal bool AddIfNotPresent(T value, out int location)
    {
        if (_buckets.IsNullOrEmpty()) Initialize(0);
        Debug.Assert(_buckets.IsNullOrEmpty() == false);

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
            var i = bucket - 1; // Value in _buckets is 1-based
            if (typeof(T).IsValueType)

                // ValueType: Devirtualize with EqualityComparer<TValue>.Default intrinsic
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

                        // The chain of entries forms a loop, which means a concurrent update has happened.
                        ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }
            else
            {
                // Object type: Shared Generic, EqualityComparer<TValue>.Default won't devirtualize (https://github.com/dotnet/runtime/issues/10050),
                // so cache in a local rather than get EqualityComparer per loop iteration.
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

                        // The chain of entries forms a loop, which means a concurrent update has happened.
                        ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }
            }
        }
        else
        {
            hashCode = value is not null ? comparer.GetHashCode(value) : 0;
            bucket = ref GetBucketRef(hashCode);
            var i = bucket - 1; // Value in _buckets is 1-based
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

                    // The chain of entries forms a loop, which means a concurrent update has happened.
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
            entry.Next = bucket - 1; // Value in _buckets is 1-based
            entry.Value = value;
            bucket = index + 1;
            _version++;
            location = index;
        }

        // Value types never rehash
        if (!typeof(T).IsValueType &&
            collisionCount > HashHelpers.HashCollisionThreshold &&
            ReferenceEquals(comparer, _stringComparer))
        {
            // If we hit the collision threshold we'll need to switch to the comparer which is using randomized string hashing
            // i.e. EqualityComparer<string>.Default.
            Resize(entries.Length, true);
            location = FindItemIndex(value);
            Debug.Assert(location >= 0);
        }

        return true;
    }

    /// <summary>
    ///     Checks if this contains of other's elements. Iterates over other's elements and returns false as soon as it finds
    ///     an element in other that's not in this. Used by SupersetOf, ProperSupersetOf, and SetEquals.
    /// </summary>
    bool ContainsAllElements(IEnumerable<T> other)
    {
        foreach (var element in other)
            if (!Contains(element))
                return false;

        return true;
    }

    /// <summary>
    ///     Implementation Notes: If other is a hashset and is using same equality comparer, then checking subset is faster.
    ///     Simply check that each element in this is in other. Note: if other doesn't use same equality comparer, then Contains
    ///     check is invalid, which is why callers must take are of this. If callers are concerned about whether this is a proper
    ///     subset, they take care of that.
    /// </summary>
    internal bool IsSubsetOfHashSetWithSameComparer(TempHashSet<T> other)
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

    /// <summary>
    ///     Implementation Notes: If other is a hashset and is using same equality comparer, then checking subset is faster.
    ///     Simply check that each element in this is in other. Note: if other doesn't use same equality comparer, then Contains
    ///     check is invalid, which is why callers must take are of this. If callers are concerned about whether this is a proper
    ///     subset, they take care of that.
    /// </summary>
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

    /// <summary>
    ///     If other is a hashset that uses same equality comparer, intersect is much faster because we can use other's
    ///     Contains
    /// </summary>
    internal void IntersectWithHashSetWithSameComparer(TempHashSet<T> other)
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

    /// <summary>
    ///     If other is a hashset that uses same equality comparer, intersect is much faster because we can use other's
    ///     Contains
    /// </summary>
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

    /// <summary>
    ///     Iterate over other. If contained in this, mark an element in bit array corresponding to its position in _slots. If
    ///     anything is unmarked (in bit array), remove it. This attempts to allocate on the stack, if below StackAllocThreshold.
    /// </summary>
    void IntersectWithEnumerable(IEnumerable<T> other)
    {
        Debug.Assert(_buckets.IsNullOrEmpty() == false, "_buckets shouldn't be null; callers should check first");

        // Keep track of current last index; don't want to move past the end of our bit array
        // (could happen if another thread is modifying the collection).
        var originalCount = _count;
        var intArrayLength = BitHelper.ToIntArrayLength(originalCount);

        int[] pooledArray = null;
        BitHelper bitHelper = new(intArrayLength <= StackAllocThreshold ?
                stackalloc int[intArrayLength] :
                new(pooledArray = _bucketPool.Rent(intArrayLength), 0, 100),
            true);

        // Mark if contains: find index of in slots array and mark corresponding element in bit array.
        foreach (var item in other)
        {
            var index = FindItemIndex(item);
            if (index >= 0) bitHelper.MarkBit(index);
        }

        // If anything unmarked, remove it. Perf can be optimized here if BitHelper had a
        // FindFirstUnmarked method.
        for (var i = 0; i < originalCount; i++)
        {
            ref var entry = ref _entries![i];
            if (entry.Next >= -1 && !bitHelper.IsMarked(i)) Remove(entry.Value);
        }

        if (pooledArray is not null) _bucketPool.Return(pooledArray);
    }

    /// <summary>
    ///     if other is a set, we can assume it doesn't have duplicate elements, so use this technique: if can't remove, then
    ///     it wasn't present in this set, so add. As with other methods, callers take care of ensuring that other is a hashset
    ///     using the same equality comparer.
    /// </summary>
    /// <param name="other"> </param>
    void SymmetricExceptWithUniqueHashSet(TempHashSet<T> other)
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

    /// <summary>
    ///     if other is a set, we can assume it doesn't have duplicate elements, so use this technique: if can't remove, then
    ///     it wasn't present in this set, so add. As with other methods, callers take care of ensuring that other is a hashset
    ///     using the same equality comparer.
    /// </summary>
    /// <param name="other"> </param>
    void SymmetricExceptWithUniqueHashSet(HashSet<T> other)
    {
        foreach (var item in other)
            if (!Remove(item))
                AddIfNotPresent(item, out _);
    }

    /// <summary>
    ///     Implementation notes: Used for symmetric except when other isn't a HashSet. This is more tedious because other may
    ///     contain duplicates. HashSet technique could fail in these situations: 1. Other has a duplicate that's not in this:
    ///     HashSet technique would add then remove it. 2. Other has a duplicate that's in this: HashSet technique would remove then
    ///     add it back. In general, its presence would be toggled each time it appears in other. This technique uses bit marking to
    ///     indicate whether to add/remove the item. If already present in collection, it will get marked for deletion. If added
    ///     from other, it will get marked as something not to remove.
    /// </summary>
    /// <param name="other"> </param>
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
            if (AddIfNotPresent(item, out var location))

                // wasn't already present in collection; flag it as something not to remove
                // *NOTE* if location is out of range, we should ignore. BitHelper will
                // detect that it's out of bounds and not try to mark it. But it's
                // expected that location could be out of bounds because adding the item
                // will increase _lastIndex as soon as all the free spots are filled.
                itemsAddedFromOther.MarkBit(location);
            else
            {
                // already there...if not added from other, mark for remove.
                // *NOTE* Even though BitHelper will check that location is in range, we want
                // to check here. There's no point in checking items beyond originalCount
                // because they could not have been in the original collection
                if (location < originalCount && !itemsAddedFromOther.IsMarked(location)) itemsToRemove.MarkBit(location);
            }

        // if anything marked, remove it
        for (var i = 0; i < originalCount; i++)
            if (itemsToRemove.IsMarked(i))
                Remove(_entries![i].Value);

        if (itemsToRemoveArray is null) return;

        _bucketPool.Return(itemsToRemoveArray);
        _bucketPool.Return(itemsAddedFromOtherArray);
    }

    /// <summary>
    ///     Determines counts that can be used to determine equality, subset, and superset. This is only used when other is an
    ///     IEnumerable and not a HashSet. If other is a HashSet these properties can be checked faster without use of marking
    ///     because we can assume other has no duplicates. The following count checks are performed by callers: 1. Equals: checks if
    ///     unfoundCount = 0 and uniqueFoundCount = _count; i.e. everything in other is in this and everything in this is in other
    ///     2. Subset: checks if unfoundCount >= 0 and uniqueFoundCount = _count; i.e. other may have elements not in this and
    ///     everything in this is in other 3. Proper subset: checks if unfoundCount > 0 and uniqueFoundCount = _count; i.e other
    ///     must have at least one element not in this and everything in this is in other 4. Proper superset: checks if unfound
    ///     count = 0 and uniqueFoundCount strictly less than _count; i.e. everything in other was in this and this had at least one
    ///     element not contained in other. An earlier implementation used delegates to perform these checks rather than returning
    ///     an ElementCount struct; however this was changed due to the perf overhead of delegates.
    /// </summary>
    /// <param name="other"> </param>
    /// <param name="returnIfUnfound"> Allows us to finish faster for equals and proper superset because unfoundCount must be 0. </param>
    (int UniqueCount, int UnfoundCount) CheckUniqueAndUnfoundElements(IEnumerable<T> other, bool returnIfUnfound)
    {
        // Need special case in case this has no elements.
        if (_count == 0)
        {
            var numElementsInOther = 0;
            foreach (var item in other)
            {
                numElementsInOther++;
                break; // break right away, all we want to know is whether other has 0 or 1 elements
            }

            return (UniqueCount: 0, UnfoundCount: numElementsInOther);
        }

        Debug.Assert(_buckets.IsNullOrEmpty() == false && _count > 0, "_buckets was null but count greater than 0");

        var originalCount = _count;
        var intArrayLength = BitHelper.ToIntArrayLength(originalCount);

        int[] pooledArray = null;
        BitHelper bitHelper = new(intArrayLength <= StackAllocThreshold ?
                stackalloc int[intArrayLength] :
                new(pooledArray = _bucketPool.Rent(intArrayLength), 0, 100),
            true);

        var unfoundCount = 0; // count of items in other not found in this
        var uniqueFoundCount = 0; // count of unique items in other found in this

        foreach (var item in other)
        {
            var index = FindItemIndex(item);
            if (index >= 0)
            {
                if (bitHelper.IsMarked(index)) continue;

                // Item hasn't been seen yet.
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

    /// <summary>
    ///     Checks if equality comparers are equal. This is used for algorithms that can speed up if it knows the other item
    ///     has unique elements. I.e. if they're using different equality comparers, then uniqueness assumption between sets break.
    /// </summary>
    internal static bool EqualityComparersAreEqual(TempHashSet<T> set1, TempHashSet<T> set2)
        => set1.Comparer.Equals(set2.Comparer);

    /// <summary>
    ///     Checks if equality comparers are equal. This is used for algorithms that can speed up if it knows the other item
    ///     has unique elements. I.e. if they're using different equality comparers, then uniqueness assumption between sets break.
    /// </summary>
    internal static bool EqualityComparersAreEqual(TempHashSet<T> set1, HashSet<T> set2)
        => set1.Comparer.Equals(set2.Comparer);

    /// <summary>
    ///     Checks if equality comparers are equal. This is used for algorithms that can speed up if it knows the other item
    ///     has unique elements. I.e. if they're using different equality comparers, then uniqueness assumption between sets break.
    /// </summary>
    internal static bool EqualityComparersAreEqual(HashSet<T> set1, TempHashSet<T> set2)
        => set1.Comparer.Equals(set2.Comparer);

    void RenewBuckets(int newSize)
    {
        if (_buckets is not null) _bucketPool.Return(_buckets);

        var buckets = _bucketPool.Rent(newSize);
        Array.Clear(buckets, 0, buckets.Length);
        _buckets = buckets;
    }

    void RenewEntries(int newSize)
    {
        if (_entries is not null) _entryPool.Return(_entries, s_clearEntries);

        _entries = _entryPool.Rent(newSize);
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

            // Use unsigned comparison since we set index to dictionary.count+1 when the enumeration ends.
            // dictionary.count+1 could be negative if dictionary.count is int.MaxValue
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
    }

    internal TempHashSet(T[] items) : this(items.AsSpan()) { }

    internal TempHashSet(T[] items, IEqualityComparer<T> comparer) : this(items.AsSpan(),
        comparer,
        ArrayPool<int>.Shared,
        ArrayPool<Entry<T>>.Shared) { }

    internal TempHashSet(T[] items, IEqualityComparer<T> comparer, ArrayPool<int> bucketPool, ArrayPool<Entry<T>> entryPool)
        : this(items.AsSpan(), comparer, bucketPool, entryPool) { }

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
        if (index >= 0) return ref _entries![index].Value;

        return ref Unsafe.NullRef<T>();
    }

    /// <summary> Take the union of this PooledSet with other. Modifies this set. </summary>
    /// <param name="other"> </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnionWith(T[] other) => UnionWith((ReadOnlySpan<T>)other);

    /// <summary> Take the union of this PooledSet with other. Modifies this set. </summary>
    /// <param name="other"> enumerable with items to add </param>
    public void UnionWith(ReadOnlySpan<T> other)
    {
        for (int i = 0, len = other.Length; i < len; i++) AddIfNotPresent(other[i], out _);
    }

    /// <summary> Takes the intersection of this set with other. Modifies this set. </summary>
    /// <remarks>
    ///     Implementation Notes: Iterate over the other and mark intersection by checking contains in this. Then loop over
    ///     and delete any unmarked elements. Total cost is n2+n1. Attempts to return early based on counts alone, using the
    ///     property that the intersection of anything with the empty set is the empty set.
    /// </remarks>
    /// <param name="other"> enumerable with items to add </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IntersectWith(T[] other) => IntersectWith((ReadOnlySpan<T>)other);

    /// <summary> Takes the intersection of this set with other. Modifies this set. </summary>
    /// <remarks>
    ///     Implementation Notes: Iterate over the other and mark intersection by checking contains in this. Then loop over
    ///     and delete any unmarked elements. Total cost is n2+n1. Attempts to return early based on counts alone, using the
    ///     property that the intersection of anything with the empty set is the empty set.
    /// </remarks>
    /// <param name="other"> enumerable with items to add </param>
    public void IntersectWith(ReadOnlySpan<T> other)
    {
        // intersection of anything with empty set is empty set, so return if count is 0
        if (_count == 0) return;

        // if other is empty, intersection is empty set; remove all elements and we're done
        if (other.Length == 0)
        {
            Clear();
            return;
        }

        IntersectWithSpan(other);
    }

    /// <summary>
    ///     Iterate over other. If contained in this, mark an element in bit array corresponding to its position in _slots. If
    ///     anything is unmarked (in bit array), remove it. This attempts to allocate on the stack, if below StackAllocThreshold.
    /// </summary>
    /// <param name="other"> </param>
    void IntersectWithSpan(ReadOnlySpan<T> other)
    {
        Debug.Assert(_buckets.IsNullOrEmpty() == false, "_buckets shouldn't be null; callers should check first");

        // keep track of current last index; don't want to move past the end of our bit array
        // (could happen if another thread is modifying the collection)
        var originalCount = _count;
        var intArrayLength = BitHelper.ToIntArrayLength(originalCount);

        Span<int> span = stackalloc int[StackAllocThreshold];
        var bitHelper = intArrayLength <= StackAllocThreshold ?
            new BitHelper(span.Slice(0, intArrayLength), true) :
            new BitHelper(new int[intArrayLength], false);

        // mark if contains: find index of in slots array and mark corresponding element in bit array
        for (int i = 0, len = other.Length; i < len; i++)
        {
            var index = FindItemIndex(other[i]);
            if (index >= 0) bitHelper.MarkBit(index);
        }

        // If anything unmarked, remove it. Perf can be optimized here if BitHelper had a
        // FindFirstUnmarked method.
        for (var i = 0; i < originalCount; i++)
        {
            ref var entry = ref _entries![i];
            if (entry.Next >= -1 && !bitHelper.IsMarked(i)) Remove(entry.Value);
        }
    }

    /// <summary> Remove items in other from this set. Modifies this set. </summary>
    /// <param name="other"> enumerable with items to remove </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExceptWith(T[] other) => ExceptWith((ReadOnlySpan<T>)other);

    /// <summary> Remove items in other from this set. Modifies this set. </summary>
    /// <param name="other"> enumerable with items to remove </param>
    public void ExceptWith(ReadOnlySpan<T> other)
    {
        // this is already the empty set; return
        if (_count == 0) return;

        // remove every element in other from this
        for (int i = 0, len = other.Length; i < len; i++) Remove(other[i]);
    }

    /// <summary> Takes symmetric difference (XOR) with other and this set. Modifies this set. </summary>
    /// <param name="other"> array with items to XOR </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SymmetricExceptWith(T[] other) => SymmetricExceptWith((ReadOnlySpan<T>)other);

    /// <summary> Takes symmetric difference (XOR) with other and this set. Modifies this set. </summary>
    /// <param name="other"> span with items to XOR </param>
    public void SymmetricExceptWith(ReadOnlySpan<T> other)
    {
        // if set is empty, then symmetric difference is other
        if (_count == 0)
        {
            UnionWith(other);
            return;
        }

        SymmetricExceptWithSpan(other);
    }

    /// <summary>
    ///     Implementation notes: Used for symmetric except when other isn't a HashSet. This is more tedious because other may
    ///     contain duplicates. HashSet technique could fail in these situations: 1. Other has a duplicate that's not in this:
    ///     HashSet technique would add then remove it. 2. Other has a duplicate that's in this: HashSet technique would remove then
    ///     add it back. In general, its presence would be toggled each time it appears in other. This technique uses bit marking to
    ///     indicate whether to add/remove the item. If already present in collection, it will get marked for deletion. If added
    ///     from other, it will get marked as something not to remove.
    /// </summary>
    /// <param name="other"> </param>
    void SymmetricExceptWithSpan(ReadOnlySpan<T> other)
    {
        var originalCount = _count;
        var intArrayLength = BitHelper.ToIntArrayLength(originalCount);

        Span<int> itemsToRemoveSpan = stackalloc int[StackAllocThreshold / 2];
        var itemsToRemove = intArrayLength <= StackAllocThreshold / 2 ?
            new BitHelper(itemsToRemoveSpan.Slice(0, intArrayLength), true) :
            new BitHelper(new int[intArrayLength], false);

        Span<int> itemsAddedFromOtherSpan = stackalloc int[StackAllocThreshold / 2];
        var itemsAddedFromOther = intArrayLength <= StackAllocThreshold / 2 ?
            new BitHelper(itemsAddedFromOtherSpan.Slice(0, intArrayLength), true) :
            new BitHelper(new int[intArrayLength], false);

        for (int i = 0, len = other.Length; i < len; i++)
            if (AddIfNotPresent(other[i], out var location))

                // wasn't already present in collection; flag it as something not to remove
                // *NOTE* if location is out of range, we should ignore. BitHelper will
                // detect that it's out of bounds and not try to mark it. But it's
                // expected that location could be out of bounds because adding the item
                // will increase _lastIndex as soon as all the free spots are filled.
                itemsAddedFromOther.MarkBit(location);
            else
            {
                // already there...if not added from other, mark for remove.
                // *NOTE* Even though BitHelper will check that location is in range, we want
                // to check here. There's no point in checking items beyond originalCount
                // because they could not have been in the original collection
                if (location < originalCount && !itemsAddedFromOther.IsMarked(location)) itemsToRemove.MarkBit(location);
            }

        // if anything marked, remove it
        for (var i = 0; i < originalCount; i++)
            if (itemsToRemove.IsMarked(i))
                Remove(_entries![i].Value);
    }

    /// <summary> Checks if this is a subset of other. </summary>
    /// <param name="other"> </param>
    /// <returns> true if this is a subset of other; false if not </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSubsetOf(T[] other) => IsSubsetOf((ReadOnlySpan<T>)other);

    /// <summary> Checks if this is a subset of other. </summary>
    /// <param name="other"> </param>
    /// <returns> true if this is a subset of other; false if not </returns>
    public bool IsSubsetOf(ReadOnlySpan<T> other)
    {
        // The empty set is a subset of any set
        if (_count == 0) return true;

        var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, false);
        return uniqueCount == Count && unfoundCount >= 0;
    }

    /// <summary> Checks if this is a proper subset of other (i.e. strictly contained in) </summary>
    /// <remarks>
    ///     Implementation Notes: The following properties are used up-front to avoid element-wise checks: 1. If this is the
    ///     empty set, then it's a proper subset of a set that contains at least one element, but it's not a proper subset of the
    ///     empty set.
    /// </remarks>
    /// <param name="other"> </param>
    /// <returns> true if this is a proper subset of other; false if not </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsProperSubsetOf(T[] other) => IsProperSubsetOf((ReadOnlySpan<T>)other);

    /// <summary> Checks if this is a proper subset of other (i.e. strictly contained in) </summary>
    /// <remarks>
    ///     Implementation Notes: The following properties are used up-front to avoid element-wise checks: 1. If this is the
    ///     empty set, then it's a proper subset of a set that contains at least one element, but it's not a proper subset of the
    ///     empty set.
    /// </remarks>
    /// <param name="other"> </param>
    /// <returns> true if this is a proper subset of other; false if not </returns>
    public bool IsProperSubsetOf(ReadOnlySpan<T> other)
    {
        // no set is a proper subset of an empty set
        if (other.Length == 0) return false;

        // the empty set is a proper subset of anything but the empty set
        if (_count == 0) return other.Length > 0;

        var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, false);
        return uniqueCount == Count && unfoundCount > 0;
    }

    /// <summary>
    ///     Determines counts that can be used to determine equality, subset, and superset. This is only used when other is an
    ///     IEnumerable and not a HashSet. If other is a HashSet these properties can be checked faster without use of marking
    ///     because we can assume other has no duplicates. The following count checks are performed by callers: 1. Equals: checks if
    ///     unfoundCount = 0 and uniqueFoundCount = _count; i.e. everything in other is in this and everything in this is in other
    ///     2. Subset: checks if unfoundCount >= 0 and uniqueFoundCount = _count; i.e. other may have elements not in this and
    ///     everything in this is in other 3. Proper subset: checks if unfoundCount > 0 and uniqueFoundCount = _count; i.e other
    ///     must have at least one element not in this and everything in this is in other 4. Proper superset: checks if unfound
    ///     count = 0 and uniqueFoundCount strictly less than _count; i.e. everything in other was in this and this had at least one
    ///     element not contained in other. An earlier implementation used delegates to perform these checks rather than returning
    ///     an ElementCount struct; however this was changed due to the perf overhead of delegates.
    /// </summary>
    /// <param name="other"> </param>
    /// <param name="returnIfUnfound"> Allows us to finish faster for equals and proper superset because unfoundCount must be 0. </param>
    (int UniqueCount, int UnfoundCount) CheckUniqueAndUnfoundElements(ReadOnlySpan<T> other, bool returnIfUnfound)
    {
        // Need special case in case this has no elements.
        if (_count == 0)
        {
            var numElementsInOther = 0;
            foreach (var _ in other)
            {
                numElementsInOther++;
                break; // break right away, all we want to know is whether other has 0 or 1 elements
            }

            return (UniqueCount: 0, UnfoundCount: numElementsInOther);
        }

        Debug.Assert(_buckets.IsNullOrEmpty() == false && _count > 0, "_buckets was null but count greater than 0");

        var originalCount = _count;
        var intArrayLength = BitHelper.ToIntArrayLength(originalCount);

        Span<int> span = stackalloc int[StackAllocThreshold];
        var bitHelper = intArrayLength <= StackAllocThreshold ?
            new BitHelper(span.Slice(0, intArrayLength), true) :
            new BitHelper(new int[intArrayLength], false);

        var unfoundCount = 0; // count of items in other not found in this
        var uniqueFoundCount = 0; // count of unique items in other found in this

        for (int i = 0, len = other.Length; i < len; i++)
        {
            var index = FindItemIndex(other[i]);
            if (index >= 0)
            {
                if (bitHelper.IsMarked(index)) continue;

                // Item hasn't been seen yet.
                bitHelper.MarkBit(index);
                uniqueFoundCount++;
            }
            else
            {
                unfoundCount++;
                if (returnIfUnfound) break;
            }
        }

        return (uniqueFoundCount, unfoundCount);
    }

    /// <summary> Checks if this is a superset of other </summary>
    /// <remarks>
    ///     Implementation Notes: The following properties are used up-front to avoid element-wise checks: 1. If other has no
    ///     elements (it's the empty set), then this is a superset, even if this is also the empty set.
    /// </remarks>
    /// <param name="other"> </param>
    /// <returns> true if this is a superset of other; false if not </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSupersetOf(T[] other) => IsSupersetOf((ReadOnlySpan<T>)other);

    /// <summary> Checks if this is a superset of other </summary>
    /// <remarks>
    ///     Implementation Notes: The following properties are used up-front to avoid element-wise checks: 1. If other has no
    ///     elements (it's the empty set), then this is a superset, even if this is also the empty set.
    /// </remarks>
    /// <param name="other"> </param>
    /// <returns> true if this is a superset of other; false if not </returns>
    public bool IsSupersetOf(ReadOnlySpan<T> other)
    {
        // if other is the empty set then this is a superset
        if (other.Length == 0) return true;

        return ContainsAllElements(other);
    }

    /// <summary> Checks if this is a proper superset of other (i.e. other strictly contained in this) </summary>
    /// <remarks>
    ///     Implementation Notes: This is slightly more complicated than IsSupersetOf because we have to keep track if there
    ///     was at least one element not contained in other. The following properties are used up-front to avoid element-wise
    ///     checks: 1. If this is the empty set, then it can't be a proper superset of any set, even if other is the empty set. 2.
    ///     If other is an empty set and this contains at least 1 element, then this is a proper superset.
    /// </remarks>
    /// <param name="other"> </param>
    /// <returns> true if this is a proper superset of other; false if not </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsProperSupersetOf(T[] other) => IsProperSupersetOf((ReadOnlySpan<T>)other);

    /// <summary> Checks if this is a proper superset of other (i.e. other strictly contained in this) </summary>
    /// <remarks>
    ///     Implementation Notes: This is slightly more complicated than IsSupersetOf because we have to keep track if there
    ///     was at least one element not contained in other. The following properties are used up-front to avoid element-wise
    ///     checks: 1. If this is the empty set, then it can't be a proper superset of any set, even if other is the empty set. 2.
    ///     If other is an empty set and this contains at least 1 element, then this is a proper superset.
    /// </remarks>
    /// <param name="other"> </param>
    /// <returns> true if this is a proper superset of other; false if not </returns>
    public bool IsProperSupersetOf(ReadOnlySpan<T> other)
    {
        // the empty set isn't a proper superset of any set.
        if (_count == 0) return false;

        if (other.Length == 0)

            // note that this has at least one element, based on above check
            return true;

        var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, false);
        return uniqueCount < Count && unfoundCount == 0;
    }

    /// <summary> Checks if this set overlaps other (i.e. they share at least one item) </summary>
    /// <param name="other"> </param>
    /// <returns> true if these have at least one common element; false if disjoint </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Overlaps(T[] other) => Overlaps((ReadOnlySpan<T>)other);

    /// <summary> Checks if this set overlaps other (i.e. they share at least one item) </summary>
    /// <param name="other"> </param>
    /// <returns> true if these have at least one common element; false if disjoint </returns>
    public bool Overlaps(ReadOnlySpan<T> other)
    {
        if (_count == 0) return false;

        for (int i = 0, len = other.Length; i < len; i++)
            if (Contains(other[i]))
                return true;

        return false;
    }

    /// <summary> Checks if this and other contain the same elements. This is set equality: duplicates and order are ignored </summary>
    /// <param name="other"> </param>
    /// <returns> </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetEquals(T[] other) => SetEquals((ReadOnlySpan<T>)other);

    /// <summary> Checks if this and other contain the same elements. This is set equality: duplicates and order are ignored </summary>
    /// <param name="other"> </param>
    /// <returns> </returns>
    public bool SetEquals(ReadOnlySpan<T> other)
    {
        // if this count is 0 but other contains at least one element, they can't be equal
        if (_count == 0 && other.Length > 0) return false;

        var (uniqueCount, unfoundCount) = CheckUniqueAndUnfoundElements(other, true);
        return uniqueCount == Count && unfoundCount == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in Span<T> span) => CopyTo(span, 0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in Span<T> dest, int destIndex) => CopyTo(dest, destIndex, Count);

    public void CopyTo(in Span<T> dest, int destIndex, int count)
    {
        // Check array index valid index into array.
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        // Also throw if count less than 0.
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        // Will the array, starting at arrayIndex, be able to hold elements? Note: not
        // checking arrayIndex >= array.Length (consistency with list of allowing
        // count of 0; subsequent check takes care of the rest)
        if (dest.Length - destIndex < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);

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

    /// <summary>
    ///     Checks if this contains of other's elements. Iterates over other's elements and returns false as soon as it finds
    ///     an element in other that's not in this. Used by SupersetOf, ProperSupersetOf, and SetEquals.
    /// </summary>
    /// <param name="other"> </param>
    /// <returns> </returns>
    bool ContainsAllElements(ReadOnlySpan<T> other)
    {
        for (int i = 0, len = other.Length; i < len; i++)
            if (!Contains(other[i]))
                return false;

        return true;
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

    void ReturnEntries(Entry<T>[] replaceWith)
    {
        if (_entries is not null)
            try
            {
                _entryPool.Return(_entries, s_clearEntries);
            }
            catch { }

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