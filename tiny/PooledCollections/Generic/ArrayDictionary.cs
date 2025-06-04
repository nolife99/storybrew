// https://github.com/sebas77/Svelto.Common/blob/master/DataStructures/Dictionaries/SveltoDictionary.cs

#pragma warning disable CS8632

namespace Tiny.PooledCollections.Generic;

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

/// <summary>
///     Dictionary that <typeparamref name="TKey"/> and <typeparamref name="TValue"/> are stored in dense arrays.
///     Effectively, internal <see cref="Keys"/> and <see cref="Values"/> can be iterated over like normal arrays.
/// </summary>
/// <remarks>To iterate over <see cref="Keys"/> or <see cref="Values"/> as arrays, they must be get through unsafe APIs.</remarks>
[Serializable]
public class ArrayDictionary<TKey, TValue>
    : IArrayDictionary<TKey, TValue>, ISerializable, IDeserializationCallback, IDisposable where TKey : notnull
{
    // constants for serialization
    const string CountName = "Count"; // Do not rename (binary serialization). Must save buckets.Length
    const string KeyValuePairsName = "KeyValuePairs"; // Do not rename (binary serialization)

    internal static readonly bool s_clearEntries = SystemRuntimeHelpers.IsReferenceOrContainsReferences<TKey>();
    internal static readonly bool s_clearValues = SystemRuntimeHelpers.IsReferenceOrContainsReferences<TValue>();

    static readonly Type s_typeOfKey = typeof(TKey);
    static readonly ArrayEntry<TKey>[] s_emptyEntries = [];
    static readonly TValue[] s_emptyValues = [];
    static readonly int[] s_emptyBuckets = [];
    [NonSerialized] internal ArrayPool<int> _bucketPool;
    internal int[] _buckets;
    internal int _collisions;

    internal ArrayEntry<TKey>[] _entries;

    [NonSerialized] internal ArrayPool<ArrayEntry<TKey>> _entryPool;
    internal ulong _fastModBucketsMultiplier;

    internal int _freeEntryIndex;
    [NonSerialized] internal ArrayPool<TValue> _valuePool;
    internal TValue[] _values;

    public ArrayDictionary() : this(0,
        ArrayPool<ArrayEntry<TKey>>.Shared,
        ArrayPool<TValue>.Shared,
        ArrayPool<int>.Shared) { }

    public ArrayDictionary(int capacity) : this(capacity,
        ArrayPool<ArrayEntry<TKey>>.Shared,
        ArrayPool<TValue>.Shared,
        ArrayPool<int>.Shared) { }

    public ArrayDictionary(int capacity,
        ArrayPool<ArrayEntry<TKey>> entryPool,
        ArrayPool<TValue> valuePool,
        ArrayPool<int> bucketPool)
    {
        if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);

        _entryPool = entryPool ?? ArrayPool<ArrayEntry<TKey>>.Shared;
        _valuePool = valuePool ?? ArrayPool<TValue>.Shared;
        _bucketPool = bucketPool ?? ArrayPool<int>.Shared;

        Initialize(capacity);

        if (capacity > 0) _fastModBucketsMultiplier = HashHelpers.GetFastModMultiplier((uint)capacity);
    }

    public ArrayDictionaryKeyCollection<TKey, TValue> Keys
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(this);
    }

    public ArrayDictionaryValueCollection<TKey, TValue> Values
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(this);
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

    public TValue this[in TKey key]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _values[GetIndex(in key)];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            TryGetIndex(in key, out var index);

            _values[index] = value;
        }
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _freeEntryIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TKey key, TValue value)
    {
        var ret = TryGetIndex(key, out var index);

#if DEBUG
        if (ret == false) ThrowHelper.ThrowAddingDuplicateWithKeyArgumentException(key);
#endif

        _values[index] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TKey key, in TValue value)
    {
        var ret = TryGetIndex(key, out var index);

#if DEBUG
        if (ret == false) ThrowHelper.ThrowAddingDuplicateWithKeyArgumentException(key);
#endif

        _values[index] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(in TKey key, TValue value)
    {
        var ret = TryGetIndex(in key, out var index);

#if DEBUG
        if (ret == false) ThrowHelper.ThrowAddingDuplicateWithKeyArgumentException(key);
#endif

        _values[index] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(in TKey key, in TValue value)
    {
        var ret = TryGetIndex(in key, out var index);

#if DEBUG
        if (ret == false) ThrowHelper.ThrowAddingDuplicateWithKeyArgumentException(key);
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
    public bool TryAdd(TKey key, in TValue value, out int index)
    {
        var ret = TryGetIndex(key, out index);

        if (ret) _values[index] = value;

        return ret;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(in TKey key, TValue value, out int index)
    {
        var ret = TryGetIndex(in key, out index);

        if (ret) _values[index] = value;

        return ret;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(in TKey key, in TValue value, out int index)
    {
        var ret = TryGetIndex(in key, out index);

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
    public void Set(TKey key, in TValue value)
    {
        var ret = TryGetIndex(key, out var index);

#if DEBUG
        if (ret == true) throw new InvalidOperationException("Try to set value on an unexisting key.");
#endif

        _values[index] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(in TKey key, TValue value)
    {
        var ret = TryGetIndex(in key, out var index);

#if DEBUG
        if (ret == true) throw new InvalidOperationException("Try to set value on an unexisting key.");
#endif

        _values[index] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(in TKey key, in TValue value)
    {
        var ret = TryGetIndex(in key, out var index);

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

        //Buckets cannot be FastCleared because it's important that the values are reset to 0
        Array.Clear(_buckets, 0, _buckets.Length);

        if (s_clearEntries) Array.Clear(_entries, 0, _entries.Length);

        if (s_clearValues) Array.Clear(_values, 0, _values.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    //WARNING this method must stay stateless (not relying on states that can change, it's ok to read
    //constant states) because it will be used in multithreaded parallel code
    public bool ContainsKey(TKey key) => TryFindIndex(key, out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    //WARNING this method must stay stateless (not relying on states that can change, it's ok to read
    //constant states) because it will be used in multithreaded parallel code
    public bool ContainsKey(in TKey key) => TryFindIndex(in key, out _);

    public bool ContainsValue(TValue value)
    {
        var values = _values;

        if (value == null)
        {
            foreach (var item in values)
                if (item == null)
                    return true;
        }
        else if (typeof(TValue).IsValueType)
        {
            // ValueType: Devirtualize with EqualityComparer<TValue>.Default intrinsic
            foreach (var item in values)
                if (EqualityComparer<TValue>.Default.Equals(item, value))
                    return true;
        }
        else
        {
            // Object type: Shared Generic, EqualityComparer<TValue>.Default won't devirtualize
            // https://github.com/dotnet/runtime/issues/10050
            // So cache in a local rather than get EqualityComparer per loop iteration
            var defaultComparer = EqualityComparer<TValue>.Default;
            foreach (var item in values)
                if (defaultComparer.Equals(item, value))
                    return true;
        }

        return false;
    }

    public bool ContainsValue(in TValue value)
    {
        var values = _values;

        if (value == null)
        {
            foreach (var item in values)
                if (item == null)
                    return true;
        }
        else if (typeof(TValue).IsValueType)
        {
            // ValueType: Devirtualize with EqualityComparer<TValue>.Default intrinsic
            foreach (var item in values)
                if (EqualityComparer<TValue>.Default.Equals(item, value))
                    return true;
        }
        else
        {
            // Object type: Shared Generic, EqualityComparer<TValue>.Default won't devirtualize
            // https://github.com/dotnet/runtime/issues/10050
            // So cache in a local rather than get EqualityComparer per loop iteration
            var defaultComparer = EqualityComparer<TValue>.Default;
            foreach (var item in values)
                if (defaultComparer.Equals(item, value))
                    return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    //WARNING this method must stay stateless (not relying on states that can change, it's ok to read
    //constant states) because it will be used in multithreaded parallel code
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    //WARNING this method must stay stateless (not relying on states that can change, it's ok to read
    //constant states) because it will be used in multithreaded parallel code
    public bool TryGetValue(in TKey key, out TValue result)
    {
        if (TryFindIndex(in key, out var findIndex))
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

        //Burst is not able to vectorise code if throw is found, regardless if it's actually ever thrown
        TryFindIndex(key, out var findIndex);

        return findIndex;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex(in TKey key)
    {
#if DEBUG
        if (TryFindIndex(in key, out var findIndex) == true) return findIndex;

        ThrowHelper.ThrowKeyNotFoundException(key);
        return default;
#else

        //Burst is not able to vectorise code if throw is found, regardless if it's actually ever thrown
        TryFindIndex(in key, out var findIndex);

        return findIndex;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(TKey key) => Remove(key, out _, out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(in TKey key) => Remove(in key, out _, out _);

    public bool Remove(TKey key, out int index, out TValue value)
    {
        var hash = key.GetHashCode();
        var bucketIndex = Reduce((uint)hash, (uint)_buckets.Length, _fastModBucketsMultiplier);

        //find the bucket
        var indexToValueToRemove = _buckets[bucketIndex] - 1;

        //Part one: look for the actual key in the bucket list if found I update the bucket list so that it doesn't
        //point anymore to the cell to remove
        while (indexToValueToRemove != -1)
        {
            ref var entry = ref _entries[indexToValueToRemove];
            if (entry.Hashcode == hash && EqualityComparer<TKey>.Default.Equals(entry.Key, key))
            {
                //if the key is found and the bucket points directly to the node to remove
                if (_buckets[bucketIndex] - 1 == indexToValueToRemove)
                {
#if DEBUG
                    if (entry.Next != -1) throw new InvalidOperationException("If the bucket points to the cell, next MUST NOT exists");
#endif

                    //the bucket will point to the previous cell. if a previous cell exists
                    //its next pointer must be updated!
                    //<--- iteration order
                    //                      Bucket points always to the last one
                    //   ------- ------- -------
                    //   |  1  | |  2  | |  3  | //bucket cannot have next, only previous
                    //   ------- ------- -------
                    //--> insert order
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
            return false; //not found!
        }

        index = indexToValueToRemove;

        _freeEntryIndex--; //one less value to iterate
        value = _values[indexToValueToRemove];

        //Part two:
        //At this point nodes pointers and buckets are updated, but the _values array
        //still has got the value to delete. Remember the goal of this dictionary is to be able
        //to iterate over the values like an array, so the values array must always be up to date

        //if the cell to remove is the last one in the list, we can perform less operations (no swapping needed)
        //otherwise we want to move the last value cell over the value to remove
        if (indexToValueToRemove != _freeEntryIndex)
        {
            //we can move the last value of both arrays in place of the one to delete.
            //in order to do so, we need to be sure that the bucket pointer is updated.
            //first we find the index in the bucket list of the pointer that points to the cell
            //to move
            ref var entry = ref _entries[_freeEntryIndex];
            var movingBucketIndex = Reduce((uint)entry.Hashcode, (uint)_buckets.Length, _fastModBucketsMultiplier);

            //if the key is found and the bucket points directly to the node to remove
            //it must now point to the cell where it's going to be moved
            if (_buckets[movingBucketIndex] - 1 == _freeEntryIndex) _buckets[movingBucketIndex] = indexToValueToRemove + 1;

            //otherwise it means that there was more than one key with the same hash (collision), so
            //we need to update the linked list and its pointers
            var next = entry.Next;
            var previous = entry.Previous;

            //they now point to the cell where the last value is moved into
            if (next != -1) _entries[next].Previous = indexToValueToRemove;
            if (previous != -1) _entries[previous].Next = indexToValueToRemove;

            //finally, actually move the values
            _entries[indexToValueToRemove] = entry;
            _values[indexToValueToRemove] = _values[_freeEntryIndex];
        }

        return true;
    }

    public bool Remove(in TKey key, out int index, out TValue value)
    {
        var hash = key.GetHashCode();
        var bucketIndex = Reduce((uint)hash, (uint)_buckets.Length, _fastModBucketsMultiplier);

        //find the bucket
        var indexToValueToRemove = _buckets[bucketIndex] - 1;

        //Part one: look for the actual key in the bucket list if found I update the bucket list so that it doesn't
        //point anymore to the cell to remove
        while (indexToValueToRemove != -1)
        {
            ref var entry = ref _entries[indexToValueToRemove];
            if (entry.Hashcode == hash && EqualityComparer<TKey>.Default.Equals(entry.Key, key))
            {
                //if the key is found and the bucket points directly to the node to remove
                if (_buckets[bucketIndex] - 1 == indexToValueToRemove)
                {
#if DEBUG
                    if (entry.Next != -1) throw new InvalidOperationException("If the bucket points to the cell, next MUST NOT exists");
#endif

                    //the bucket will point to the previous cell. if a previous cell exists
                    //its next pointer must be updated!
                    //<--- iteration order
                    //                      Bucket points always to the last one
                    //   ------- ------- -------
                    //   |  1  | |  2  | |  3  | //bucket cannot have next, only previous
                    //   ------- ------- -------
                    //--> insert order
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
            return false; //not found!
        }

        index = indexToValueToRemove;

        _freeEntryIndex--; //one less value to iterate
        value = _values[indexToValueToRemove];

        //Part two:
        //At this point nodes pointers and buckets are updated, but the _values array
        //still has got the value to delete. Remember the goal of this dictionary is to be able
        //to iterate over the values like an array, so the values array must always be up to date

        //if the cell to remove is the last one in the list, we can perform less operations (no swapping needed)
        //otherwise we want to move the last value cell over the value to remove
        if (indexToValueToRemove != _freeEntryIndex)
        {
            //we can move the last value of both arrays in place of the one to delete.
            //in order to do so, we need to be sure that the bucket pointer is updated.
            //first we find the index in the bucket list of the pointer that points to the cell
            //to move
            ref var entry = ref _entries[_freeEntryIndex];
            var movingBucketIndex = Reduce((uint)entry.Hashcode, (uint)_buckets.Length, _fastModBucketsMultiplier);

            //if the key is found and the bucket points directly to the node to remove
            //it must now point to the cell where it's going to be moved
            if (_buckets[movingBucketIndex] - 1 == _freeEntryIndex) _buckets[movingBucketIndex] = indexToValueToRemove + 1;

            //otherwise it means that there was more than one key with the same hash (collision), so
            //we need to update the linked list and its pointers
            var next = entry.Next;
            var previous = entry.Previous;

            //they now point to the cell where the last value is moved into
            if (next != -1) _entries[next].Previous = indexToValueToRemove;
            if (previous != -1) _entries[previous].Next = indexToValueToRemove;

            //finally, actually move the values
            _entries[indexToValueToRemove] = entry;
            _values[indexToValueToRemove] = _values[_freeEntryIndex];
        }

        return true;
    }

    //I store all the index with an offset + 1, so that in the bucket list 0 means actually not existing.
    //When read the offset must be offset by -1 again to be the real one. In this way
    //I avoid to initialize the array to -1

    //WARNING this method must stay stateless (not relying on states that can change, it's ok to read
    //constant states) because it will be used in multithreaded parallel code
    public bool TryFindIndex(TKey key, out int findIndex)
    {
        var hash = key.GetHashCode();

        var bucketIndex = (int)Reduce((uint)hash, (uint)_buckets.Length, _fastModBucketsMultiplier);

        var valueIndex = _buckets[bucketIndex] - 1;

        //even if we found an existing value we need to be sure it's the one we requested
        while (valueIndex != -1)
        {
            ref var entry = ref _entries[valueIndex];
            if (entry.Hashcode == hash && EqualityComparer<TKey>.Default.Equals(entry.Key, key))
            {
                //this is the one
                findIndex = valueIndex;
                return true;
            }

            valueIndex = entry.Previous;
        }

        findIndex = 0;
        return false;
    }

    //I store all the index with an offset + 1, so that in the bucket list 0 means actually not existing.
    //When read the offset must be offset by -1 again to be the real one. In this way
    //I avoid to initialize the array to -1

    //WARNING this method must stay stateless (not relying on states that can change, it's ok to read
    //constant states) because it will be used in multithreaded parallel code
    public bool TryFindIndex(in TKey key, out int findIndex)
    {
        var hash = key.GetHashCode();

        var bucketIndex = (int)Reduce((uint)hash, (uint)_buckets.Length, _fastModBucketsMultiplier);

        var valueIndex = _buckets[bucketIndex] - 1;

        //even if we found an existing value we need to be sure it's the one we requested
        while (valueIndex != -1)
        {
            ref var entry = ref _entries[valueIndex];
            if (entry.Hashcode == hash && EqualityComparer<TKey>.Default.Equals(entry.Key, key))
            {
                //this is the one
                findIndex = valueIndex;
                return true;
            }

            valueIndex = entry.Previous;
        }

        findIndex = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(KeyValuePair<TKey, TValue>[] dest, int destIndex) => CopyTo(dest.AsSpan(), destIndex, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator IEnumerable.GetEnumerator() => new KeyValuePairEnumerator(this);

    bool ICollection<ArrayKeyValuePair<TKey, TValue>>.IsReadOnly => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ICollection<ArrayKeyValuePair<TKey, TValue>>.Add(ArrayKeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool ICollection<ArrayKeyValuePair<TKey, TValue>>.Contains(ArrayKeyValuePair<TKey, TValue> item)
        => ContainsKey(item.Key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ICollection<ArrayKeyValuePair<TKey, TValue>>.CopyTo(ArrayKeyValuePair<TKey, TValue>[] dest, int destIndex)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        if (dest.Length - destIndex < Count) ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);

        var keys = _entries.AsSpan();
        var values = _values ?? s_emptyValues;

        if (keys.Length == 0 || values.Length == 0) return;

        for (int i = 0, len = Count; i < len; i++)
            dest[destIndex++] = new ArrayKeyValuePair<TKey, TValue>(keys[i].Key, values, i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool ICollection<ArrayKeyValuePair<TKey, TValue>>.Remove(ArrayKeyValuePair<TKey, TValue> item) => Remove(item.Key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator<ArrayKeyValuePair<TKey, TValue>> IEnumerable<ArrayKeyValuePair<TKey, TValue>>.GetEnumerator()
        => new Enumerator(this);

    ICollection<TKey> IDictionary<TKey, TValue>.Keys
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new ArrayDictionaryKeyCollection<TKey, TValue>(this);
    }

    ICollection<TValue> IDictionary<TKey, TValue>.Values
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new ArrayDictionaryValueCollection<TKey, TValue>(this);
    }

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new ArrayDictionaryKeyCollection<TKey, TValue>(this);
    }

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new ArrayDictionaryValueCollection<TKey, TValue>(this);
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => ContainsKey(item.Key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        => new KeyValuePairEnumerator(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(KeyValuePair<TKey, TValue>[] dest) => CopyTo(dest.AsSpan(), 0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(KeyValuePair<TKey, TValue>[] dest, int destIndex, int count)
        => CopyTo(dest.AsSpan(), destIndex, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in Span<KeyValuePair<TKey, TValue>> dest) => CopyTo(dest, 0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in Span<KeyValuePair<TKey, TValue>> dest, int destIndex) => CopyTo(dest, destIndex, Count);

    public void CopyTo(in Span<KeyValuePair<TKey, TValue>> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        if (count < 0) ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_NeedNonNegNum();

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

    public virtual void OnDeserialization(object sender)
    {
        HashHelpers.SerializationInfoTable.TryGetValue(this, out var siInfo);

        if (siInfo == null)

            // We can return immediately if this function is called twice.
            // Note we remove the serialization info from the table at the end of this method.
            return;

        var count = siInfo.GetInt32(CountName);

        if (count > 0)
        {
            Resize(Count, count);

            var array = (KeyValuePair<TKey, TValue>[]?)siInfo.GetValue(KeyValuePairsName,
                typeof(KeyValuePair<TKey, TValue>[]));

            if (array == null) ThrowHelper.ThrowSerializationException(ExceptionResource.Serialization_MissingKeys);

            for (var i = 0; i < array.Length; i++)
            {
                if (array[i].Key == null) ThrowHelper.ThrowSerializationException(ExceptionResource.Serialization_NullKey);

                Add(array[i].Key, array[i].Value);
            }
        }

        HashHelpers.SerializationInfoTable.Remove(this);
    }

    public void Dispose()
    {
        ReturnBuckets(s_emptyBuckets);
        ReturnEntries(s_emptyEntries);
        ReturnValues(s_emptyValues);
    }

    public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        if (info == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.info);

        var count = Count;

        info.AddValue(CountName, count);

        if (count > 0)
        {
            var array = new KeyValuePair<TKey, TValue>[count];
            CopyTo(array);
            info.AddValue(KeyValuePairsName, array, typeof(KeyValuePair<TKey, TValue>[]));
        }
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(this);

    bool TryGetIndex(TKey key, out int index)
    {
        var hash = key.GetHashCode(); //IEquatable doesn't enforce the override of GetHashCode
        var bucketIndex = (int)Reduce((uint)hash, (uint)_buckets.Length, _fastModBucketsMultiplier);

        //buckets value -1 means it's empty
        var valueIndex = _buckets[bucketIndex] - 1;

        if (valueIndex == -1)
        {
            ResizeIfNeeded();

            //create the info node at the last position and fill it with the relevant information
            _entries[_freeEntryIndex] = new ArrayEntry<TKey>(key, hash);
        }
        else //collision or already exists
        {
            if (s_typeOfKey.IsValueType)
            {
                var currentValueIndex = valueIndex;
                do
                {
                    //must check if the key already exists in the dictionary
                    //ValueType: Devirtualize with EqualityComparer<TValue>.Default intrinsic, since .NET Core 2.1

                    ref var entry = ref _entries[currentValueIndex];
                    if (entry.Hashcode == hash && EqualityComparer<TKey>.Default.Equals(entry.Key, key))
                    {
                        //the key already exists, simply replace the value!
                        index = currentValueIndex;
                        return false;
                    }

                    currentValueIndex = entry.Previous;
                }
                while (currentValueIndex != -1); //-1 means no more values with key with the same hash
            }
            else
            {
                // Object type: Shared Generic, EqualityComparer<TValue>.Default won't devirtualize
                // https://github.com/dotnet/runtime/issues/10050
                // So cache in a local rather than get EqualityComparer per loop iteration
                var defaultComparer = EqualityComparer<TKey>.Default;

                var currentValueIndex = valueIndex;
                do
                {
                    //must check if the key already exists in the dictionary

                    ref var entry = ref _entries[currentValueIndex];
                    if (entry.Hashcode == hash && defaultComparer.Equals(entry.Key, key))
                    {
                        //the key already exists, simply replace the value!
                        index = currentValueIndex;
                        return false;
                    }

                    currentValueIndex = entry.Previous;
                }
                while (currentValueIndex != -1); //-1 means no more values with key with the same hash
            }

            ResizeIfNeeded();

            //oops collision!
            _collisions++;

            //create a new node which previous index points to node currently pointed in the bucket
            _entries[_freeEntryIndex] = new ArrayEntry<TKey>(key, hash, valueIndex);

            //update the next of the existing cell to point to the new one
            //old one -> new one | old one <- next one
            _entries[valueIndex].Next = _freeEntryIndex;

            //Important: the new node is always the one that will be pointed by the bucket cell
            //so I can assume that the one pointed by the bucket is always the last value added
            //(next = -1)
        }

        //item with this bucketIndex will point to the last value created
        //ToDo: if instead I assume that the original one is the one in the bucket
        //I wouldn't need to update the bucket here. Small optimization but important
        _buckets[bucketIndex] = _freeEntryIndex + 1;

        index = _freeEntryIndex;
        _freeEntryIndex++;

        //too many collisions?
        if (_collisions > _buckets.Length)
        {
            //we need more space and less collisions
            RenewBuckets(HashHelpers.ExpandPrime(_collisions));
            _collisions = 0;
            _fastModBucketsMultiplier = HashHelpers.GetFastModMultiplier((uint)_buckets.Length);

            //we need to get all the hash code of all the values stored so far and spread them over the new bucket
            //length
            for (var newValueIndex = 0; newValueIndex < _freeEntryIndex; newValueIndex++)
            {
                //get the original hash code and find the new bucketIndex due to the new length
                ref var entry = ref _entries[newValueIndex];
                bucketIndex = (int)Reduce((uint)entry.Hashcode, (uint)_buckets.Length, _fastModBucketsMultiplier);

                //bucketsIndex can be -1 or a next value. If it's -1 means no collisions. If there is collision,
                //we create a new node which prev points to the old one. Old one next points to the new one.
                //the bucket will now points to the new one
                //In this way we can rebuild the linkedlist.
                //get the current valueIndex, it's -1 if no collision happens
                var existingValueIndex = _buckets[bucketIndex] - 1;

                //update the bucket index to the index of the current item that share the bucketIndex
                //(last found is always the one in the bucket)
                _buckets[bucketIndex] = newValueIndex + 1;
                if (existingValueIndex != -1)
                {
                    //oops a value was already being pointed by this cell in the new bucket list,
                    //it means there is a collision, problem
                    _collisions++;

                    //the bucket will point to this value, so
                    //the previous index will be used as previous for the new value.
                    entry.Previous = existingValueIndex;
                    entry.Next = -1;

                    //and update the previous next index to the new one
                    _entries[existingValueIndex].Next = newValueIndex;
                }
                else
                {
                    //ok nothing was indexed, the bucket was empty. We need to update the previous
                    //values of next and previous
                    entry.Next = -1;
                    entry.Previous = -1;
                }
            }
        }

        return true;
    }

    bool TryGetIndex(in TKey key, out int index)
    {
        var hash = key.GetHashCode(); //IEquatable doesn't enforce the override of GetHashCode
        var bucketIndex = (int)Reduce((uint)hash, (uint)_buckets.Length, _fastModBucketsMultiplier);

        //buckets value -1 means it's empty
        var valueIndex = _buckets[bucketIndex] - 1;

        if (valueIndex == -1)
        {
            ResizeIfNeeded();

            //create the info node at the last position and fill it with the relevant information
            _entries[_freeEntryIndex] = new ArrayEntry<TKey>(in key, hash);
        }
        else //collision or already exists
        {
            if (s_typeOfKey.IsValueType)
            {
                var currentValueIndex = valueIndex;
                do
                {
                    //must check if the key already exists in the dictionary
                    //ValueType: Devirtualize with EqualityComparer<TValue>.Default intrinsic, since .NET Core 2.1

                    ref var entry = ref _entries[currentValueIndex];
                    if (entry.Hashcode == hash && EqualityComparer<TKey>.Default.Equals(entry.Key, key))
                    {
                        //the key already exists, simply replace the value!
                        index = currentValueIndex;
                        return false;
                    }

                    currentValueIndex = entry.Previous;
                }
                while (currentValueIndex != -1); //-1 means no more values with key with the same hash
            }
            else
            {
                // Object type: Shared Generic, EqualityComparer<TValue>.Default won't devirtualize
                // https://github.com/dotnet/runtime/issues/10050
                // So cache in a local rather than get EqualityComparer per loop iteration
                var defaultComparer = EqualityComparer<TKey>.Default;

                var currentValueIndex = valueIndex;
                do
                {
                    //must check if the key already exists in the dictionary

                    ref var entry = ref _entries[currentValueIndex];
                    if (entry.Hashcode == hash && defaultComparer.Equals(entry.Key, key))
                    {
                        //the key already exists, simply replace the value!
                        index = currentValueIndex;
                        return false;
                    }

                    currentValueIndex = entry.Previous;
                }
                while (currentValueIndex != -1); //-1 means no more values with key with the same hash
            }

            ResizeIfNeeded();

            //oops collision!
            _collisions++;

            //create a new node which previous index points to node currently pointed in the bucket
            _entries[_freeEntryIndex] = new ArrayEntry<TKey>(in key, hash, valueIndex);

            //update the next of the existing cell to point to the new one
            //old one -> new one | old one <- next one
            _entries[valueIndex].Next = _freeEntryIndex;

            //Important: the new node is always the one that will be pointed by the bucket cell
            //so I can assume that the one pointed by the bucket is always the last value added
            //(next = -1)
        }

        //item with this bucketIndex will point to the last value created
        //ToDo: if instead I assume that the original one is the one in the bucket
        //I wouldn't need to update the bucket here. Small optimization but important
        _buckets[bucketIndex] = _freeEntryIndex + 1;

        index = _freeEntryIndex;
        _freeEntryIndex++;

        //too many collisions?
        if (_collisions > _buckets.Length)
        {
            //we need more space and less collisions
            RenewBuckets(HashHelpers.ExpandPrime(_collisions));
            _collisions = 0;
            _fastModBucketsMultiplier = HashHelpers.GetFastModMultiplier((uint)_buckets.Length);

            //we need to get all the hash code of all the values stored so far and spread them over the new bucket
            //length
            for (var newValueIndex = 0; newValueIndex < _freeEntryIndex; newValueIndex++)
            {
                //get the original hash code and find the new bucketIndex due to the new length
                ref var entry = ref _entries[newValueIndex];
                bucketIndex = (int)Reduce((uint)entry.Hashcode, (uint)_buckets.Length, _fastModBucketsMultiplier);

                //bucketsIndex can be -1 or a next value. If it's -1 means no collisions. If there is collision,
                //we create a new node which prev points to the old one. Old one next points to the new one.
                //the bucket will now points to the new one
                //In this way we can rebuild the linkedlist.
                //get the current valueIndex, it's -1 if no collision happens
                var existingValueIndex = _buckets[bucketIndex] - 1;

                //update the bucket index to the index of the current item that share the bucketIndex
                //(last found is always the one in the bucket)
                _buckets[bucketIndex] = newValueIndex + 1;
                if (existingValueIndex != -1)
                {
                    //oops a value was already being pointed by this cell in the new bucket list,
                    //it means there is a collision, problem
                    _collisions++;

                    //the bucket will point to this value, so
                    //the previous index will be used as previous for the new value.
                    entry.Previous = existingValueIndex;
                    entry.Next = -1;

                    //and update the previous next index to the new one
                    _entries[existingValueIndex].Next = newValueIndex;
                }
                else
                {
                    //ok nothing was indexed, the bucket was empty. We need to update the previous
                    //values of next and previous
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

                if (values.IsNullOrEmpty() == false) _valuePool.Return(values, s_clearValues);
            }
            else _valuePool.Return(newValues);
        }

        var entries = _entries;

        if (newCapacity > entries.Length)
        {
            var newEntries = _entryPool.Rent(newCapacity);

            if (newEntries.Length > entries.Length)
            {
                if (count > 0) Array.Copy(entries, newEntries, count);

                _entries = newEntries;

                if (entries.IsNullOrEmpty() == false) _entryPool.Return(entries, s_clearEntries);
            }
            else _entryPool.Return(newEntries);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrimExcess() => Resize(Count, Count);

    void RenewBuckets(int newSize)
    {
        if (_buckets.IsNullOrEmpty() == false)
            try
            {
                _bucketPool.Return(_buckets);
            }
            catch { }

        var buckets = _bucketPool.Rent(newSize);
        Array.Clear(buckets, 0, buckets.Length);
        _buckets = buckets;
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

    void ReturnEntries(ArrayEntry<TKey>[] replaceWith)
    {
        if (_entries.IsNullOrEmpty() == false)
            try
            {
                _entryPool.Return(_entries, s_clearEntries);
            }
            catch { }

        _entries = replaceWith ?? s_emptyEntries;
    }

    void ReturnValues(TValue[] replaceWith)
    {
        if (_values.IsNullOrEmpty() == false)
            try
            {
                _valuePool.Return(_values, s_clearValues);
            }
            catch { }

        _values = replaceWith ?? s_emptyValues;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint Reduce(uint hashcode, uint N, ulong fastModBucketsMultiplier)
    {
        if (hashcode >= N) //is the condition return actually an optimization?
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetOrAdd(TKey key)
    {
        if (TryFindIndex(key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(key, out findIndex);

        _values[findIndex] = default;

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetOrAdd(in TKey key)
    {
        if (TryFindIndex(in key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(in key, out findIndex);

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
    public ref TValue GetOrAdd(in TKey key, Func<TValue> builder)
    {
        if (TryFindIndex(in key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(in key, out findIndex);

        _values[findIndex] = builder();

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetOrAdd<W>(TKey key, FuncRef<W, TValue> builder, ref W parameter)
    {
        if (TryFindIndex(key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(key, out findIndex);

        _values[findIndex] = builder(ref parameter);

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetOrAdd<W>(in TKey key, FuncRef<W, TValue> builder, ref W parameter)
    {
        if (TryFindIndex(in key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(in key, out findIndex);

        _values[findIndex] = builder(ref parameter);

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue RecycleOrAdd<TValueProxy>(TKey key, Func<TValueProxy> builder, ActionRef<TValueProxy> recycler)
        where TValueProxy : class, TValue
    {
        if (TryFindIndex(key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(key, out findIndex);

        if (_values[findIndex] == null) _values[findIndex] = builder();
        else recycler(ref Unsafe.As<TValue, TValueProxy>(ref _values[findIndex]));

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue RecycleOrAdd<TValueProxy>(in TKey key, Func<TValueProxy> builder, ActionRef<TValueProxy> recycler)
        where TValueProxy : class, TValue
    {
        if (TryFindIndex(in key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(in key, out findIndex);

        if (_values[findIndex] == null) _values[findIndex] = builder();
        else recycler(ref Unsafe.As<TValue, TValueProxy>(ref _values[findIndex]));

        return ref _values[findIndex];
    }

    /// <summary>
    ///     RecycledOrCreate makes sense to use on dictionaries that are fast cleared and use objects as value. Once the
    ///     dictionary is fast cleared, it will try to reuse object values that are recycled during the fast clearing.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="builder"></param>
    /// <param name="recycler"></param>
    /// <param name="parameter"></param>
    /// <typeparam name="TValueProxy"></typeparam>
    /// <typeparam name="U"></typeparam>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue RecycleOrAdd<TValueProxy, U>(TKey key,
        FuncRef<U, TValue> builder,
        ActionRef<TValueProxy, U> recycler,
        ref U parameter) where TValueProxy : class, TValue
    {
        if (TryFindIndex(key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(key, out findIndex);

        if (_values[findIndex] == null) _values[findIndex] = builder(ref parameter);
        else recycler(ref Unsafe.As<TValue, TValueProxy>(ref _values[findIndex]), ref parameter);

        return ref _values[findIndex];
    }

    /// <summary>
    ///     RecycledOrCreate makes sense to use on dictionaries that are fast cleared and use objects as value. Once the
    ///     dictionary is fast cleared, it will try to reuse object values that are recycled during the fast clearing.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="builder"></param>
    /// <param name="recycler"></param>
    /// <param name="parameter"></param>
    /// <typeparam name="TValueProxy"></typeparam>
    /// <typeparam name="U"></typeparam>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue RecycleOrAdd<TValueProxy, U>(in TKey key,
        FuncRef<U, TValue> builder,
        ActionRef<TValueProxy, U> recycler,
        ref U parameter) where TValueProxy : class, TValue
    {
        if (TryFindIndex(in key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(in key, out findIndex);

        if (_values[findIndex] == null) _values[findIndex] = builder(ref parameter);
        else recycler(ref Unsafe.As<TValue, TValueProxy>(ref _values[findIndex]), ref parameter);

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    //WARNING this method must stay stateless (not relying on states that can change, it's ok to read
    //constant states) because it will be used in multi-threaded parallel code
    public ref TValue GetIndexedValueByRef(int index) => ref _values[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueByRef(TKey key)
    {
#if DEBUG
        if (TryFindIndex(key, out var findIndex) == true) return ref _values[findIndex];

        ThrowHelper.ThrowKeyNotFoundException(key);
        return ref Unsafe.NullRef<TValue>();
#else

        //Burst is not able to vectorise code if throw is found, regardless if it's actually ever thrown
        TryFindIndex(key, out var findIndex);

        return ref _values[findIndex];
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueByRef(in TKey key)
    {
#if DEBUG
        if (TryFindIndex(in key, out var findIndex) == true) return ref _values[findIndex];

        ThrowHelper.ThrowKeyNotFoundException(key);
        return ref Unsafe.NullRef<TValue>();
#else

        //Burst is not able to vectorise code if throw is found, regardless if it's actually ever thrown
        TryFindIndex(in key, out var findIndex);

        return ref _values[findIndex];
#endif
    }

    public struct Enumerator : IEnumerator<ArrayKeyValuePair<TKey, TValue>>
    {
        readonly ArrayDictionary<TKey, TValue> _dictionary;

#if DEBUG
        private int _startCount;
#endif

        int _count;
        int _index;

        public Enumerator(ArrayDictionary<TKey, TValue> dictionary)
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
            if (_index < _count - 1)
            {
                ++_index;
                return true;
            }

            return false;
        }

        public ArrayKeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(_dictionary._entries[_index].Key, _dictionary._values, _index);
        }

        object IEnumerator.Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new ArrayKeyValuePair<TKey, TValue>(_dictionary._entries[_index].Key, _dictionary._values, _index);
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

    struct KeyValuePairEnumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        readonly ArrayDictionary<TKey, TValue> _dictionary;

#if DEBUG
        private int _startCount;
#endif

        readonly int _count;
        int _index;

        public KeyValuePairEnumerator(ArrayDictionary<TKey, TValue> dictionary)
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
            if (_index < _count - 1)
            {
                ++_index;
                return true;
            }

            return false;
        }

        public KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(_dictionary._entries[_index].Key, _dictionary._values[_index]);
        }

        object IEnumerator.Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new KeyValuePair<TKey, TValue>(_dictionary._entries[_index].Key, _dictionary._values[_index]);
        }

        public void Reset() => _index = -1;

        public void Dispose() { }
    }
}