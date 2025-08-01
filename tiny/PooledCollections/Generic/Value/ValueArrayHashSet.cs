// https://github.com/sebas77/Svelto.Common/blob/master/DataStructures/Dictionaries/SveltoDictionary.cs

namespace Tiny.PooledCollections.Generic.Value;

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public struct ValueArrayHashSet<T> : IArrayHashSet<T>, IDisposable where T : notnull
{
    internal ArrayEntry<T>[] _entries;
    internal int[] _buckets;

    internal int _freeEntryIndex;
    internal int _collisions;
    internal ulong _fastModBucketsMultiplier;

    internal readonly ArrayPool<ArrayEntry<T>> _entryPool;
    internal readonly ArrayPool<int> _bucketPool;

    internal static readonly bool s_clearEntries = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    static readonly Type s_typeOfKey = typeof(T);
    static readonly ArrayEntry<T>[] s_emptyEntries = [];
    static readonly int[] s_emptyBuckets = [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArrayHashSet<T> Create() => new(0, ArrayPool<ArrayEntry<T>>.Shared, ArrayPool<int>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArrayHashSet<T> Create(int capacity)
        => new(capacity, ArrayPool<ArrayEntry<T>>.Shared, ArrayPool<int>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArrayHashSet<T> Create(int capacity, ArrayPool<ArrayEntry<T>> entryPool, ArrayPool<int> bucketPool)
        => new(capacity, entryPool, bucketPool);

    internal ValueArrayHashSet(int capacity, ArrayPool<ArrayEntry<T>> entryPool, ArrayPool<int> bucketPool) : this()
    {
        if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);

        _entryPool = entryPool ?? ArrayPool<ArrayEntry<T>>.Shared;
        _bucketPool = bucketPool ?? ArrayPool<int>.Shared;

        Initialize(capacity);

        if (capacity > 0) _fastModBucketsMultiplier = HashHelpers.GetFastModMultiplier((uint)capacity);
    }

    void Initialize(int capacity)
    {
        capacity = HashHelpers.GetPrime(capacity);

        var buckets = _bucketPool.Rent(capacity);
        Array.Clear(buckets, 0, buckets.Length);

        _buckets = buckets;
        _entries = _entryPool.Rent(capacity);
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _freeEntryIndex;
    }

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _entries is not null && _buckets is not null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Add(T item) => TryGetIndex(item, out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Add(T item, out int index) => TryGetIndex(item, out index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        if (_freeEntryIndex == 0) return;

        _freeEntryIndex = 0;

        Array.Clear(_buckets, 0, _buckets.Length);

        if (s_clearEntries) Array.Clear(_entries, 0, _entries.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item) => TryFindIndex(item, out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(in T item) => TryFindIndex(in item, out _);

    public void EnsureCapacity(int capacity)
    {
        if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);

        if (_entries.Length < capacity) Resize(Count, HashHelpers.ExpandPrime(capacity));
    }

    public void IncreaseCapacityBy(int capacity)
    {
        if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);

        Resize(Count, HashHelpers.ExpandPrime(_entries.Length + capacity));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex(T item)
    {
#if DEBUG
        if (TryFindIndex(item, out var findIndex) == true) return findIndex;

        ThrowHelper.ThrowKeyNotFoundException(item);
        return default;
#else

        TryFindIndex(item, out var findIndex);

        return findIndex;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex(in T item)
    {
#if DEBUG
        if (TryFindIndex(in item, out var findIndex) == true) return findIndex;

        ThrowHelper.ThrowKeyNotFoundException(item);
        return default;
#else

        TryFindIndex(in item, out var findIndex);

        return findIndex;
#endif
    }

    bool TryGetIndex(T item, out int index)
    {
        var hash = item.GetHashCode();
        var bucketIndex = (int)Reduce((uint)hash, (uint)_buckets.Length, _fastModBucketsMultiplier);

        var valueIndex = _buckets[bucketIndex] - 1;

        if (valueIndex == -1)
        {
            ResizeIfNeeded();

            _entries[_freeEntryIndex] = new ArrayEntry<T>(item, hash);
        }
        else
        {
            if (s_typeOfKey.IsValueType)
            {
                var currentValueIndex = valueIndex;
                do
                {
                    ref var entry = ref _entries[currentValueIndex];
                    if (entry.Hashcode == hash && EqualityComparer<T>.Default.Equals(entry.Key, item))
                    {
                        index = currentValueIndex;
                        return false;
                    }

                    currentValueIndex = entry.Previous;
                }
                while (currentValueIndex != -1);
            }
            else
            {
                var defaultComparer = EqualityComparer<T>.Default;

                var currentValueIndex = valueIndex;
                do
                {
                    ref var entry = ref _entries[currentValueIndex];
                    if (entry.Hashcode == hash && defaultComparer.Equals(entry.Key, item))
                    {
                        index = currentValueIndex;
                        return false;
                    }

                    currentValueIndex = entry.Previous;
                }
                while (currentValueIndex != -1);
            }

            ResizeIfNeeded();

            _collisions++;

            _entries[_freeEntryIndex] = new ArrayEntry<T>(item, hash, valueIndex);

            _entries[valueIndex].Next = _freeEntryIndex;
        }

        _buckets[bucketIndex] = _freeEntryIndex + 1;

        index = _freeEntryIndex;
        _freeEntryIndex++;

        if (_collisions > _buckets.Length)
        {
            RenewBuckets(HashHelpers.ExpandPrime(_collisions));
            _collisions = 0;
            _fastModBucketsMultiplier = HashHelpers.GetFastModMultiplier((uint)_buckets.Length);

            for (var newValueIndex = 0; newValueIndex < _freeEntryIndex; newValueIndex++)
            {
                ref var entry = ref _entries[newValueIndex];
                bucketIndex = (int)Reduce((uint)entry.Hashcode, (uint)_buckets.Length, _fastModBucketsMultiplier);

                var existingValueIndex = _buckets[bucketIndex] - 1;

                _buckets[bucketIndex] = newValueIndex + 1;
                if (existingValueIndex != -1)
                {
                    _collisions++;

                    entry.Previous = existingValueIndex;
                    entry.Next = -1;

                    _entries[existingValueIndex].Next = newValueIndex;
                }
                else
                {
                    entry.Next = -1;
                    entry.Previous = -1;
                }
            }
        }

        return true;
    }

    void ResizeIfNeeded()
    {
        if (_freeEntryIndex == _entries.Length) Resize(Count, HashHelpers.ExpandPrime(_freeEntryIndex));
    }

    void Resize(int count, int newCapacity)
    {
        var entries = _entries;

        if (newCapacity > entries.Length)
        {
            var newEntries = _entryPool.Rent(newCapacity);

            if (newEntries.Length > entries.Length)
            {
                if (count > 0) Array.Copy(entries, newEntries, count);

                _entries = newEntries;

                if (entries is not null) _entryPool.Return(entries, s_clearEntries);
            }
            else _entryPool.Return(newEntries);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(T item) => Remove(item, out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(in T item) => Remove(in item, out _);

    public bool Remove(T item, out int index)
    {
        var hash = item.GetHashCode();
        var bucketIndex = Reduce((uint)hash, (uint)_buckets.Length, _fastModBucketsMultiplier);

        var indexToValueToRemove = _buckets[bucketIndex] - 1;

        while (indexToValueToRemove != -1)
        {
            ref var entry = ref _entries[indexToValueToRemove];
            if (entry.Hashcode == hash && EqualityComparer<T>.Default.Equals(entry.Key, item))
            {
                if (_buckets[bucketIndex] - 1 == indexToValueToRemove)
                {
#if DEBUG
                    if (entry.Next != -1) throw new InvalidOperationException("If the bucket points to the cell, next MUST NOT exists");
#endif

                    _buckets[bucketIndex] = entry.Previous + 1;
                }
#if DEBUG
                else
                {
                    if (entry.Next == -1) throw new InvalidOperationException("If the bucket points to another cell, next MUST exists");
                }
#endif

                UpdateLinkedList(indexToValueToRemove, ref _entries);

                break;
            }

            indexToValueToRemove = entry.Previous;
        }

        if (indexToValueToRemove == -1)
        {
            index = 0;
            return false;
        }

        index = indexToValueToRemove;

        _freeEntryIndex--;

        if (indexToValueToRemove != _freeEntryIndex)
        {
            ref var entry = ref _entries[_freeEntryIndex];
            var movingBucketIndex = Reduce((uint)entry.Hashcode, (uint)_buckets.Length, _fastModBucketsMultiplier);

            if (_buckets[movingBucketIndex] - 1 == _freeEntryIndex) _buckets[movingBucketIndex] = indexToValueToRemove + 1;

            var next = entry.Next;
            var previous = entry.Previous;

            if (next != -1) _entries[next].Previous = indexToValueToRemove;
            if (previous != -1) _entries[previous].Next = indexToValueToRemove;

            _entries[indexToValueToRemove] = entry;
        }

        return true;
    }

    public bool Remove(in T item, out int index)
    {
        var hash = item.GetHashCode();
        var bucketIndex = Reduce((uint)hash, (uint)_buckets.Length, _fastModBucketsMultiplier);

        var indexToValueToRemove = _buckets[bucketIndex] - 1;

        while (indexToValueToRemove != -1)
        {
            ref var entry = ref _entries[indexToValueToRemove];
            if (entry.Hashcode == hash && EqualityComparer<T>.Default.Equals(entry.Key, item))
            {
                if (_buckets[bucketIndex] - 1 == indexToValueToRemove)
                {
#if DEBUG
                    if (entry.Next != -1) throw new InvalidOperationException("If the bucket points to the cell, next MUST NOT exists");
#endif

                    _buckets[bucketIndex] = entry.Previous + 1;
                }
#if DEBUG
                else
                {
                    if (entry.Next == -1) throw new InvalidOperationException("If the bucket points to another cell, next MUST exists");
                }
#endif

                UpdateLinkedList(indexToValueToRemove, ref _entries);

                break;
            }

            indexToValueToRemove = entry.Previous;
        }

        if (indexToValueToRemove == -1)
        {
            index = 0;
            return false;
        }

        index = indexToValueToRemove;

        _freeEntryIndex--;

        if (indexToValueToRemove != _freeEntryIndex)
        {
            ref var entry = ref _entries[_freeEntryIndex];
            var movingBucketIndex = Reduce((uint)entry.Hashcode, (uint)_buckets.Length, _fastModBucketsMultiplier);

            if (_buckets[movingBucketIndex] - 1 == _freeEntryIndex) _buckets[movingBucketIndex] = indexToValueToRemove + 1;

            var next = entry.Next;
            var previous = entry.Previous;

            if (next != -1) _entries[next].Previous = indexToValueToRemove;
            if (previous != -1) _entries[previous].Next = indexToValueToRemove;

            _entries[indexToValueToRemove] = entry;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrimExcess() => Resize(Count, Count);

    public bool TryFindIndex(T item, out int findIndex)
    {
        var hash = item.GetHashCode();

        var bucketIndex = (int)Reduce((uint)hash, (uint)_buckets.Length, _fastModBucketsMultiplier);

        var valueIndex = _buckets[bucketIndex] - 1;

        while (valueIndex != -1)
        {
            ref var entry = ref _entries[valueIndex];
            if (entry.Hashcode == hash && EqualityComparer<T>.Default.Equals(entry.Key, item))
            {
                findIndex = valueIndex;
                return true;
            }

            valueIndex = entry.Previous;
        }

        findIndex = 0;
        return false;
    }

    public bool TryFindIndex(in T item, out int findIndex)
    {
        var hash = item.GetHashCode();

        var bucketIndex = (int)Reduce((uint)hash, (uint)_buckets.Length, _fastModBucketsMultiplier);

        var valueIndex = _buckets[bucketIndex] - 1;

        while (valueIndex != -1)
        {
            ref var entry = ref _entries[valueIndex];
            if (entry.Hashcode == hash && EqualityComparer<T>.Default.Equals(entry.Key, item))
            {
                findIndex = valueIndex;
                return true;
            }

            valueIndex = entry.Previous;
        }

        findIndex = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest) => CopyTo(dest.AsSpan(), 0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] array, int arrayIndex) => CopyTo(array.AsSpan(), arrayIndex, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest, int destIndex, int count) => CopyTo(dest.AsSpan(), destIndex, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped in Span<T> dest) => CopyTo(in dest, 0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped in Span<T> dest, int destIndex) => CopyTo(in dest, destIndex, Count);

    public void CopyTo(scoped in Span<T> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (dest.Length - destIndex < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);

        var items = _entries.AsSpan();

        if (items.Length == 0) return;

        for (int i = 0, len = Count; i < len && count > 0; i++)
        {
            dest[destIndex++] = items[i].Key;
            count--;
        }
    }

    public void Dispose()
    {
        ReturnBuckets(s_emptyBuckets);
        ReturnEntries(s_emptyEntries);
    }

    void RenewBuckets(int newSize)
    {
        if (_buckets is not null) _bucketPool.Return(_buckets);

        var buckets = _bucketPool.Rent(newSize);
        Array.Clear(buckets, 0, buckets.Length);
        _buckets = buckets;
    }

    void ReturnBuckets(int[] replaceWith)
    {
        if (_buckets is not null) _bucketPool.Return(_buckets);

        _buckets = replaceWith ?? s_emptyBuckets;
    }

    void ReturnEntries(ArrayEntry<T>[] replaceWith)
    {
        if (_entries is not null) _entryPool.Return(_entries, s_clearEntries);

        _entries = replaceWith ?? s_emptyEntries;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint Reduce(uint hashcode, uint N, ulong fastModBucketsMultiplier)
    {
        if (hashcode >= N)
            return Environment.Is64BitProcess ? HashHelpers.FastMod(hashcode, N, fastModBucketsMultiplier) : hashcode % N;

        return hashcode;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void UpdateLinkedList(int index, ref ArrayEntry<T>[] valuesInfo)
    {
        var next = valuesInfo[index].Next;
        var previous = valuesInfo[index].Previous;

        if (next != -1) valuesInfo[next].Previous = previous;
        if (previous != -1) valuesInfo[previous].Next = next;
    }

    bool ICollection<T>.IsReadOnly => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ICollection<T>.Add(T item) => Add(item);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

    public struct Enumerator : IEnumerator<T>
    {
        readonly ValueArrayHashSet<T> _set;

#if DEBUG
        private int _startCount;
#endif

        int _count;
        int _index;

        public Enumerator(in ValueArrayHashSet<T> set)
        {
            _set = set;
            _index = -1;
            _count = set.Count;
#if DEBUG
            _startCount = set.Count;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
#if DEBUG
            if (_count != _startCount) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
#endif
            if (_index < _count - 1)
            {
                ++_index;
                return true;
            }

            return false;
        }

        public T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _set._entries[_index].Key;
        }

        object IEnumerator.Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _set._entries[_index].Key;
        }

        public void SetRange(int startIndex, int count)
        {
            _index = startIndex - 1;
            _count = count;
#if DEBUG
            if (_count > _startCount) throw new InvalidOperationException("Cannot set a count greater than its starting value");

            _startCount = count;
#endif
        }

        public void Reset() => _index = -1;

        public void Dispose() { }
    }
}