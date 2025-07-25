// https://github.com/sebas77/Svelto.Common/blob/master/DataStructures/Dictionaries/SveltoDictionary.cs

namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public ref struct TempArrayDictionary<TKey, TValue> where TKey : notnull
{
    internal ArrayEntry<TKey>[] _entries;
    internal TValue[] _values;
    internal int[] _buckets;

    internal int _freeEntryIndex;
    internal int _collisions;
    internal ulong _fastModBucketsMultiplier;

    internal readonly ArrayPool<ArrayEntry<TKey>> _entryPool;
    internal readonly ArrayPool<TValue> _valuePool;
    internal readonly ArrayPool<int> _bucketPool;

    internal static readonly bool s_clearEntries = RuntimeHelpers.IsReferenceOrContainsReferences<TKey>();
    internal static readonly bool s_clearValues = RuntimeHelpers.IsReferenceOrContainsReferences<TValue>();

    static readonly Type s_typeOfKey = typeof(TKey);
    static readonly ArrayEntry<TKey>[] s_emptyEntries = [];
    static readonly TValue[] s_emptyValues = [];
    static readonly int[] s_emptyBuckets = [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArrayDictionary<TKey, TValue> Create() => new(0,
        ArrayPool<ArrayEntry<TKey>>.Shared,
        ArrayPool<TValue>.Shared,
        ArrayPool<int>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArrayDictionary<TKey, TValue> Create(int capacity) => new(capacity,
        ArrayPool<ArrayEntry<TKey>>.Shared,
        ArrayPool<TValue>.Shared,
        ArrayPool<int>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArrayDictionary<TKey, TValue> Create(int capacity,
        ArrayPool<ArrayEntry<TKey>> entryPool,
        ArrayPool<TValue> valuePool,
        ArrayPool<int> bucketPool) => new(capacity, entryPool, valuePool, bucketPool);

    internal TempArrayDictionary(int capacity,
        ArrayPool<ArrayEntry<TKey>> entryPool,
        ArrayPool<TValue> valuePool,
        ArrayPool<int> bucketPool) : this()
    {
        if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);

        _entryPool = entryPool ?? ArrayPool<ArrayEntry<TKey>>.Shared;
        _valuePool = valuePool ?? ArrayPool<TValue>.Shared;
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
        _values = _valuePool.Rent(capacity);
    }

    public TValue this[TKey key]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _values[GetIndex(key)];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            TryGetIndex(key, out var index);

            _values[index] = value;
        }
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _freeEntryIndex;
    }

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _entries is not null && _values is not null && _buckets is not null;
    }

    public TempArrayDictionaryKeyCollection<TKey, TValue> Keys
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(this);
    }

    public TempArrayDictionaryValueCollection<TKey, TValue> Values
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TKey key, TValue value)
    {
        var ret = TryGetIndex(key, out var index);

#if DEBUG
        if (!ret) ThrowHelper.ThrowAddingDuplicateWithKeyArgumentException(key);
#endif

        _values[index] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(TKey key, TValue value, out int index)
    {
        var ret = TryGetIndex(key, out index);

        if (ret) _values[index] = value;

        return ret;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(TKey key, TValue value)
    {
        var ret = TryGetIndex(key, out var index);

#if DEBUG
        if (ret == true) throw new InvalidOperationException("Try to set value on an unexisting key.");
#endif

        _values[index] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        if (_freeEntryIndex == 0) return;

        _freeEntryIndex = 0;

        Array.Clear(_buckets, 0, _buckets.Length);

        if (s_clearEntries) Array.Clear(_entries, 0, _entries.Length);

        if (s_clearValues) Array.Clear(_values, 0, _values.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool ContainsKey(TKey key) => TryFindIndex(key, out _);

    public readonly bool ContainsValue(TValue value)
    {
        var values = _values;

        if (value is null)
        {
            foreach (var item in values)
                if (item is null)
                    return true;
        }
        else if (typeof(TValue).IsValueType)
        {
            foreach (var item in values)
                if (EqualityComparer<TValue>.Default.Equals(item, value))
                    return true;
        }
        else
        {
            var defaultComparer = EqualityComparer<TValue>.Default;
            foreach (var item in values)
                if (defaultComparer.Equals(item, value))
                    return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(TKey key, out TValue result)
    {
        if (TryFindIndex(key, out var findIndex))
        {
            result = _values[findIndex];
            return true;
        }

        result = default;
        return false;
    }

    public void EnsureCapacity(int capacity)
    {
        if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);

        if (_values.Length < capacity) Resize(Count, HashHelpers.ExpandPrime(capacity));
    }

    public void IncreaseCapacityBy(int capacity)
    {
        if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);

        Resize(Count, HashHelpers.ExpandPrime(_values.Length + capacity));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex(TKey key)
    {
#if DEBUG
        if (TryFindIndex(key, out var findIndex) == true) return findIndex;

        ThrowHelper.ThrowKeyNotFoundException(key);
        return default;
#else

        TryFindIndex(key, out var findIndex);

        return findIndex;
#endif
    }

    bool TryGetIndex(TKey key, out int index)
    {
        var hash = key.GetHashCode();
        var bucketIndex = (int)Reduce((uint)hash, (uint)_buckets.Length, _fastModBucketsMultiplier);

        var valueIndex = _buckets[bucketIndex] - 1;

        if (valueIndex == -1)
        {
            ResizeIfNeeded();

            _entries[_freeEntryIndex] = new ArrayEntry<TKey>(key, hash);
        }
        else
        {
            if (s_typeOfKey.IsValueType)
            {
                var currentValueIndex = valueIndex;
                do
                {
                    ref var entry = ref _entries[currentValueIndex];
                    if (entry.Hashcode == hash && EqualityComparer<TKey>.Default.Equals(entry.Key, key))
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
                var defaultComparer = EqualityComparer<TKey>.Default;

                var currentValueIndex = valueIndex;
                do
                {
                    ref var entry = ref _entries[currentValueIndex];
                    if (entry.Hashcode == hash && defaultComparer.Equals(entry.Key, key))
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

            _entries[_freeEntryIndex] = new ArrayEntry<TKey>(key, hash, valueIndex);

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
        if (_freeEntryIndex == _values.Length) Resize(Count, HashHelpers.ExpandPrime(_freeEntryIndex));
    }

    void Resize(int count, int newCapacity)
    {
        var values = _values;

        if (newCapacity > values.Length)
        {
            var newValues = _valuePool.Rent(newCapacity);

            if (newValues.Length > values.Length)
            {
                if (count > 0) Array.Copy(values, newValues, count);

                _values = newValues;

                if (values is not null) _valuePool.Return(values, s_clearValues);
            }
            else _valuePool.Return(newValues);
        }

        var entries = _entries;

        if (newCapacity <= entries.Length) return;

        var newEntries = _entryPool.Rent(newCapacity);

        if (newEntries.Length > entries.Length)
        {
            if (count > 0) Array.Copy(entries, newEntries, count);

            _entries = newEntries;

            if (entries is not null) _entryPool.Return(entries, s_clearEntries);
        }
        else _entryPool.Return(newEntries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(TKey key) => Remove(key, out _, out _);

    public bool Remove(TKey key, out int index, out TValue value)
    {
        var hash = key.GetHashCode();
        var bucketIndex = Reduce((uint)hash, (uint)_buckets.Length, _fastModBucketsMultiplier);

        var indexToValueToRemove = _buckets[bucketIndex] - 1;

        while (indexToValueToRemove != -1)
        {
            ref var entry = ref _entries[indexToValueToRemove];
            if (entry.Hashcode == hash && EqualityComparer<TKey>.Default.Equals(entry.Key, key))
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
            value = default;
            return false;
        }

        index = indexToValueToRemove;

        _freeEntryIndex--;
        value = _values[indexToValueToRemove];

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
            _values[indexToValueToRemove] = _values[_freeEntryIndex];
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrimExcess() => Resize(Count, Count);

    public readonly bool TryFindIndex(TKey key, out int findIndex)
    {
        var hash = key.GetHashCode();

        var bucketIndex = (int)Reduce((uint)hash, (uint)_buckets.Length, _fastModBucketsMultiplier);

        var valueIndex = _buckets[bucketIndex] - 1;

        while (valueIndex != -1)
        {
            ref var entry = ref _entries[valueIndex];
            if (entry.Hashcode == hash && EqualityComparer<TKey>.Default.Equals(entry.Key, key))
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
    public void CopyTo(KeyValuePair<TKey, TValue>[] dest) => CopyTo(dest.AsSpan(), 0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(KeyValuePair<TKey, TValue>[] dest, int destIndex) => CopyTo(dest.AsSpan(), destIndex, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(KeyValuePair<TKey, TValue>[] dest, int destIndex, int count)
        => CopyTo(dest.AsSpan(), destIndex, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped Span<KeyValuePair<TKey, TValue>> dest) => CopyTo(dest, 0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped Span<KeyValuePair<TKey, TValue>> dest, int destIndex) => CopyTo(dest, destIndex, Count);

    public void CopyTo(scoped Span<KeyValuePair<TKey, TValue>> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (dest.Length - destIndex < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);

        var keys = _entries.AsSpan();
        var values = _values.AsSpan();

        if (keys.Length == 0 || values.Length == 0) return;

        for (int i = 0, len = Count; i < len && count > 0; i++)
        {
            dest[destIndex++] = new KeyValuePair<TKey, TValue>(keys[i].Key, values[i]);
            count--;
        }
    }

    public void Intersect<TOther>(TempArrayDictionary<TKey, TOther> other)
    {
        var keys = _entries;

        for (var i = Count - 1; i >= 0; i--)
        {
            var key = keys[i].Key;
            if (!other.ContainsKey(key)) Remove(key);
        }
    }

    public void Exclude<TOther>(TempArrayDictionary<TKey, TOther> otherDicKeys)
    {
        var keys = _entries;

        for (var i = Count - 1; i >= 0; i--)
        {
            var key = keys[i].Key;
            if (otherDicKeys.ContainsKey(key)) Remove(key);
        }
    }

    public void Union(TempArrayDictionary<TKey, TValue> other)
    {
        foreach (var kv in other) this[kv.Key] = kv.Value;
    }

    public void Dispose()
    {
        ReturnBuckets(s_emptyBuckets);
        ReturnEntries(s_emptyEntries);
        ReturnValues(s_emptyValues);
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

    void ReturnEntries(ArrayEntry<TKey>[] replaceWith)
    {
        if (_entries is not null) _entryPool.Return(_entries, s_clearEntries);

        _entries = replaceWith ?? s_emptyEntries;
    }

    void ReturnValues(TValue[] replaceWith)
    {
        if (_values is not null) _valuePool.Return(_values, s_clearValues);

        _values = replaceWith ?? s_emptyValues;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint Reduce(uint hashcode, uint N, ulong fastModBucketsMultiplier)
    {
        if (hashcode >= N)
            return Environment.Is64BitProcess ? HashHelpers.FastMod(hashcode, N, fastModBucketsMultiplier) : hashcode % N;

        return hashcode;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void UpdateLinkedList(int index, ref ArrayEntry<TKey>[] valuesInfo)
    {
        var next = valuesInfo[index].Next;
        var previous = valuesInfo[index].Previous;

        if (next != -1) valuesInfo[next].Previous = previous;
        if (previous != -1) valuesInfo[previous].Next = next;
    }

    public ref struct Enumerator
    {
        readonly TempArrayDictionary<TKey, TValue> _dictionary;

#if DEBUG
        int _startCount;
#endif

        int _count;
        int _index;

        internal Enumerator(TempArrayDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            _index = -1;
            _count = dictionary.Count;
#if DEBUG
            _startCount = dictionary.Count;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
#if DEBUG
            if (_count != _startCount) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
#endif
            if (_index >= _count - 1) return false;

            ++_index;
            return true;
        }

        public ArrayKeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(_dictionary._entries[_index].Key, _dictionary._values, _index);
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
    }

    ref struct KeyValuePairEnumerator
    {
        readonly TempArrayDictionary<TKey, TValue> _dictionary;

#if DEBUG
        int _startCount;
#endif

        readonly int _count;
        int _index;

        public KeyValuePairEnumerator(in TempArrayDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            _index = -1;
            _count = dictionary.Count;
#if DEBUG
            _startCount = dictionary.Count;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
#if DEBUG
            if (_count != _startCount) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
#endif
            if (_index >= _count - 1) return false;

            ++_index;
            return true;
        }

        public KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(_dictionary._entries[_index].Key, _dictionary._values[_index]);
        }

        public void Reset() => _index = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetOrAdd(TKey key)
    {
        if (TryFindIndex(key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(key, out findIndex);

        _values[findIndex] = default;

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetOrAdd(TKey key, Func<TValue> builder)
    {
        if (TryFindIndex(key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(key, out findIndex);

        _values[findIndex] = builder();

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetOrAdd<TParam>(TKey key, FuncRef<TParam, TValue> builder, ref TParam parameter)
    {
        if (TryFindIndex(key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(key, out findIndex);

        _values[findIndex] = builder(ref parameter);

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue RecycleOrAdd<TValueProxy>(TKey key, Func<TValueProxy> builder, ActionRef<TValueProxy> recycler)
        where TValueProxy : class, TValue
    {
        if (TryFindIndex(key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(key, out findIndex);

        if (_values[findIndex] is null) _values[findIndex] = builder();
        else recycler(ref Unsafe.As<TValue, TValueProxy>(ref _values[findIndex]));

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue RecycleOrAdd<TValueProxy, TParam>(TKey key,
        FuncRef<TParam, TValue> builder,
        ActionRef<TValueProxy, TParam> recycler,
        ref TParam parameter) where TValueProxy : class, TValue
    {
        if (TryFindIndex(key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(key, out findIndex);

        if (_values[findIndex] is null) _values[findIndex] = builder(ref parameter);
        else recycler(ref Unsafe.As<TValue, TValueProxy>(ref _values[findIndex]), ref parameter);

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetIndexedValueByRef(int index) => ref _values[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueByRef(TKey key)
    {
#if DEBUG
        if (TryFindIndex(key, out var findIndex) == true) return ref _values[findIndex];

        ThrowHelper.ThrowKeyNotFoundException(key);
        return ref Unsafe.NullRef<TValue>();
#else
        TryFindIndex(key, out var findIndex);

        return ref _values[findIndex];
#endif
    }
}