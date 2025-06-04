// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/List.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable CS8632

namespace Tiny.PooledCollections.Generic;

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

// Implements a variable-size List that uses an array of objects to store the
// elements. A List has a capacity, which is the allocated length
// of the internal array. As elements are added to a List, the capacity
// of the List is automatically increased as required by reallocating the
// internal array.
//
[DebuggerTypeProxy(typeof(ICollectionDebugView<>)), DebuggerDisplay("Count = {Count}"), Serializable]
public class PooledList<T> : IList<T>, IReadOnlyList<T>, IDeserializationCallback, IDisposable
{
    internal const int DefaultCapacity = 4;

    static readonly T[] s_emptyArray = [];

    internal static readonly bool s_clearItems = SystemRuntimeHelpers.IsReferenceOrContainsReferences<T>();

    internal T[] _items; // Do not rename (binary serialization)

    [NonSerialized] internal ArrayPool<T> _pool;

    internal int _size; // Do not rename (binary serialization)
    internal int _version; // Do not rename (binary serialization)

    // Constructs a List. The list is initially empty and has a capacity
    // of zero. Upon adding the first element to the list the capacity is
    // increased to DefaultCapacity, and then increased in multiples of two
    // as required.
    public PooledList() : this(ArrayPool<T>.Shared) { }

    public PooledList(int capacity) : this(capacity, ArrayPool<T>.Shared) { }

    public PooledList(IEnumerable<T> collection) : this(collection, ArrayPool<T>.Shared) { }

    public PooledList(ArrayPool<T> pool)
    {
        _items = s_emptyArray;
        _pool = pool ?? ArrayPool<T>.Shared;
    }

    // Constructs a List with a given initial capacity. The list is
    // initially empty, but will have room for the given number of elements
    // before any reallocations are required.
    //
    public PooledList(int capacity, ArrayPool<T> pool)
    {
        if (capacity < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity,
                ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

        _pool = pool ?? ArrayPool<T>.Shared;
        _items = capacity == 0 ? s_emptyArray : _pool.Rent(capacity);
    }

    // Constructs a List, copying the contents of the given collection. The
    // size and capacity of the new list will both be equal to the size of the
    // given collection.
    //
    public PooledList(IEnumerable<T> collection, ArrayPool<T> pool)
    {
        if (collection == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.collection);

        _pool = pool ?? ArrayPool<T>.Shared;

        if (collection is ICollection<T> c)
        {
            var count = c.Count;
            if (count == 0) _items = s_emptyArray;
            else
            {
                _items = _pool.Rent(count);
                c.CopyTo(_items, 0);
                _size = count;
            }
        }
        else
        {
            _size = 0;
            _items = s_emptyArray;
            using var en = collection!.GetEnumerator();
            while (en.MoveNext()) Add(en.Current);
        }
    }

    public PooledList(T[] items) : this(items.AsSpan(), ArrayPool<T>.Shared) { }

    public PooledList(T[] items, ArrayPool<T> pool) : this(items.AsSpan(), pool) { }

    public PooledList(ReadOnlySpan<T> span) : this(span, ArrayPool<T>.Shared) { }

    public PooledList(ReadOnlySpan<T> span, ArrayPool<T> pool)
    {
        _pool = pool ?? ArrayPool<T>.Shared;

        var count = span.Length;

        if (count == 0) _items = s_emptyArray;
        else
        {
            _items = _pool.Rent(count);
            span.CopyTo(_items);
            _size = count;
        }
    }

    // Gets and sets the capacity of this list.  The capacity is the size of
    // the internal array used to hold items.  When set, the internal
    // array of the list is reallocated to the given capacity.
    //
    public int Capacity
    {
        get => _items.Length;
        set
        {
            if (value < _size)
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.value,
                    ExceptionResource.ArgumentOutOfRange_SmallCapacity);

            var length = _items.Length;

            if (value == length) return;

            if (value <= 0)
            {
                ReturnArray(s_emptyArray);
                _size = 0;
                return;
            }

            var newItems = _pool.Rent(value);

            if (value < length && newItems.Length >= length)
            {
                _pool.Return(newItems);
                return;
            }

            if (_size > 0) Array.Copy(_items, newItems, _size);

            ReturnArray(newItems);
        }
    }

    void IDeserializationCallback.OnDeserialization(object sender) =>

        // We can't serialize array pools, so deserialized PooledLists will
        // have to use the shared pool, even if they were using a custom pool
        // before serialization.
        _pool = ArrayPool<T>.Shared;

    public void Dispose()
    {
        ReturnArray(s_emptyArray);
        _size = 0;
        _version++;
    }

    // Read-only property describing how many elements are in the List.
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _size;
    }

    // Is this List read-only?
    bool ICollection<T>.IsReadOnly => false;

    // Sets or Gets the element at the given index.
    public T this[int index]
    {
        get
        {
            // Following trick can reduce the range check by one
            if ((uint)index >= (uint)_size) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessException();
            return _items[index];
        }
        set
        {
            if ((uint)index >= (uint)_size) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessException();
            _items[index] = value;
            _version++;
        }
    }

    // Adds the given object to the end of this list. The size of the list is
    // increased by one. If required, the capacity of the list is doubled
    // before adding the new element.
    //
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        _version++;
        var array = _items;
        var size = _size;
        if ((uint)size < (uint)array.Length)
        {
            _size = size + 1;
            array[size] = item;
        }
        else AddWithResize(item);
    }

    // Clears the contents of List.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _version++;
        if (s_clearItems)
        {
            var size = _size;
            _size = 0;
            if (size > 0) Array.Clear(_items, 0, size); // Clear the elements so that the gc can reclaim the references.
        }
        else _size = 0;
    }

    // Contains returns true if the specified element is in the List.
    // It does a linear, O(n) search.  Equality is determined by calling
    // EqualityComparer<T>.Default.Equals().
    //
    public bool Contains(T item) =>

        // PERF: IndexOf calls Array.IndexOf, which internally
        // calls EqualityComparer<T>.Default.IndexOf, which
        // is specialized for different types. This
        // boosts performance since instead of making a
        // virtual method call each iteration of the loop,
        // via EqualityComparer<T>.Default.Equals, we
        // only make one virtual call to EqualityComparer.IndexOf.
        _size != 0 && IndexOf(item) >= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest, int destIndex) => CopyTo(0, dest, destIndex, _size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

    // Returns the index of the first occurrence of a given value in a range of
    // this list. The list is searched forwards from beginning to end.
    // The elements of the list are compared to the given value using the
    // Object.Equals method.
    //
    // This method uses the Array.IndexOf method to perform the
    // search.
    //
    public int IndexOf(T item) => Array.IndexOf(_items, item, 0, _size);

    // Inserts an element into this list at a given index. The size of the list
    // is increased by one. If required, the capacity of the list is doubled
    // before inserting the new element.
    //
    public void Insert(int index, T item)
    {
        // Note that insertions at the end are legal.
        if ((uint)index > (uint)_size)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index,
                ExceptionResource.ArgumentOutOfRange_ListInsert);

        if (_size == _items.Length) Grow(_size + 1);
        if (index < _size) Array.Copy(_items, index, _items, index + 1, _size - index);
        _items[index] = item;
        _size++;
        _version++;
    }

    // Removes the element at the given index. The size of the list is
    // decreased by one.
    public bool Remove(T item)
    {
        var index = IndexOf(item);
        if (index >= 0)
        {
            RemoveAt(index);
            return true;
        }

        return false;
    }

    // Removes the element at the given index. The size of the list is
    // decreased by one.
    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_size) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessException();
        _size--;
        if (index < _size) Array.Copy(_items, index + 1, _items, index, _size - index);
        if (s_clearItems) _items[_size] = default!;
        _version++;
    }

    // Non-inline from List.Add to improve its code quality as uncommon path
    [MethodImpl(MethodImplOptions.NoInlining)]
    void AddWithResize(T item)
    {
        SystemDebug.Assert(_size == _items.Length);
        var size = _size;
        Grow(size + 1);
        _size = size + 1;
        _items[size] = item;
    }

    // Adds the elements of the given collection to the end of this list. If
    // required, the capacity of the list is increased to twice the previous
    // capacity or the new size, whichever is larger.
    //
    public void AddRange(IEnumerable<T> collection) => InsertRange(_size, collection);

    public ReadOnlyCollection<T> AsReadOnly() => new(this);

    // Searches a section of the list for a given element using a binary search
    // algorithm. Elements of the list are compared to the search value using
    // the given IComparer interface. If comparer is null, elements of
    // the list are compared to the search value using the IComparable
    // interface, which in that case must be implemented by all elements of the
    // list and the given search value. This method assumes that the given
    // section of the list is already sorted; if this is not the case, the
    // result will be incorrect.
    //
    // The method returns the index of the given value in the list. If the
    // list does not contain the given value, the method returns a negative
    // integer. The bitwise complement operator (~) can be applied to a
    // negative result to produce the index of the first element (if any) that
    // is larger than the given search value. This is also the index at which
    // the search value should be inserted into the list in order for the list
    // to remain sorted.
    //
    // The method uses the Array.BinarySearch method to perform the
    // search.
    //
    public int BinarySearch(int index, int count, T item, IComparer<T>? comparer)
    {
        if (index < 0) ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();
        if (count < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count,
                ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

        if (_size - index < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        return Array.BinarySearch(_items, index, count, item, comparer);
    }

    public int BinarySearch(T item) => BinarySearch(0, Count, item, null);

    public int BinarySearch(T item, IComparer<T>? comparer) => BinarySearch(0, Count, item, comparer);

    public PooledList<TOut> ConvertAll<TOut>(Converter<T, TOut> converter)
    {
        if (converter == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.converter);

        var list = new PooledList<TOut>(_size);
        var src = _items;
        var dst = list._items;

        for (var i = 0; i < _size; i++) dst[i] = converter(src[i]);
        list._size = _size;
        return list;
    }

    /// <summary>Copies this List into array, which must be of a compatible array type.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest) => CopyTo(0, dest, 0, _size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public void CopyTo(int index, T[] dest, int destIndex, int count)
    {
        if (dest == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dest);

        CopyTo(index, dest.AsSpan(), destIndex, count);
    }

    /// <summary>
    ///     Ensures that the capacity of this list is at least the specified <paramref name="capacity"/>. If the current
    ///     capacity of the list is less than specified <paramref name="capacity"/>, the capacity is increased by continuously twice
    ///     current capacity until it is at least the specified <paramref name="capacity"/>.
    /// </summary>
    /// <param name="capacity">The minimum capacity to ensure.</param>
    /// <returns>The new capacity of this list.</returns>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity,
                ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

        if (_items.Length < capacity)
        {
            Grow(capacity);
            _version++;
        }

        return _items.Length;
    }

    /// <summary>Increase the capacity of this list to at least the specified <paramref name="capacity"/>.</summary>
    /// <param name="capacity">The minimum capacity to ensure.</param>
    void Grow(int capacity)
    {
        SystemDebug.Assert(_items.Length < capacity);

        var newcapacity = _items.Length == 0 ? DefaultCapacity : 2 * _items.Length;

        // Allow the list to grow to maximum possible capacity (~2G elements) before encountering overflow.
        // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
        if ((uint)newcapacity > SystemArray.MaxLength) newcapacity = SystemArray.MaxLength;

        // If the computed capacity is still less than specified, set to the original argument.
        // Capacities exceeding Array.MaxLength will be surfaced as OutOfMemoryException by Array.Resize.
        if (newcapacity < capacity) newcapacity = capacity;

        Capacity = newcapacity;
    }

    public bool Exists(Predicate<T> match) => FindIndex(match) != -1;

    public T? Find(Predicate<T> match)
    {
        if (match == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);

        var items = _items;

        for (var i = 0; i < _size; i++)
            if (match(items[i]))
                return items[i];

        return default;
    }

    public PooledList<T> FindAll(Predicate<T> match)
    {
        if (match == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);

        var list = new PooledList<T>();
        var items = _items;

        for (var i = 0; i < _size; i++)
            if (match(items[i]))
                list.Add(items[i]);

        return list;
    }

    public int FindIndex(Predicate<T> match) => FindIndex(0, _size, match);

    public int FindIndex(int startIndex, Predicate<T> match) => FindIndex(startIndex, _size - startIndex, match);

    public int FindIndex(int startIndex, int count, Predicate<T> match)
    {
        if ((uint)startIndex > (uint)_size)
            ThrowHelper.ThrowStartIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        if (count < 0 || startIndex > _size - count) ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_Count();

        if (match == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);

        var endIndex = startIndex + count;
        var items = _items;

        for (var i = startIndex; i < endIndex; i++)
            if (match(items[i]))
                return i;

        return -1;
    }

    public T? FindLast(Predicate<T> match)
    {
        if (match == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);

        var items = _items;

        for (var i = _size - 1; i >= 0; i--)
            if (match(items[i]))
                return items[i];

        return default;
    }

    public int FindLastIndex(Predicate<T> match) => FindLastIndex(_size - 1, _size, match);

    public int FindLastIndex(int startIndex, Predicate<T> match) => FindLastIndex(startIndex, startIndex + 1, match);

    public int FindLastIndex(int startIndex, int count, Predicate<T> match)
    {
        if (match == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);

        if (_size == 0)
        {
            // Special case for 0 length List
            if (startIndex != -1) ThrowHelper.ThrowStartIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLess();
        }
        else
        {
            // Make sure we're not out of range
            if ((uint)startIndex >= (uint)_size)
                ThrowHelper.ThrowStartIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLess();
        }

        // 2nd have of this also catches when startIndex == MAXINT, so MAXINT - 0 + 1 == -1, which is < 0.
        if (count < 0 || startIndex - count + 1 < 0) ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_Count();

        var endIndex = startIndex - count;
        var items = _items;

        for (var i = startIndex; i > endIndex; i--)
            if (match(items[i]))
                return i;

        return -1;
    }

    public void ForEach(Action<T> action)
    {
        if (action == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.action);

        var version = _version;
        var items = _items;

        for (var i = 0; i < _size; i++)
        {
            if (version != _version) break;

            action(items[i]);
        }

        if (version != _version) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
    }

    // Returns an enumerator for this list with the given
    // permission for removal of elements. If modifications made to the list
    // while an enumeration is in progress, the MoveNext and
    // GetObject methods of the enumerator will throw an exception.
    //
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(this);

    public PooledList<T> GetRange(int index, int count)
    {
        if (index < 0) ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();

        if (count < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count,
                ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

        if (_size - index < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        var list = new PooledList<T>(count);
        Array.Copy(_items, index, list._items, 0, count);
        list._size = count;
        return list;
    }

    // Returns the index of the first occurrence of a given value in a range of
    // this list. The list is searched forwards, starting at index
    // index and ending at count number of elements. The
    // elements of the list are compared to the given value using the
    // Object.Equals method.
    //
    // This method uses the Array.IndexOf method to perform the
    // search.
    //
    public int IndexOf(T item, int index)
    {
        if (index > _size) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessOrEqualException();
        return Array.IndexOf(_items, item, index, _size - index);
    }

    // Returns the index of the first occurrence of a given value in a range of
    // this list. The list is searched forwards, starting at index
    // index and upto count number of elements. The
    // elements of the list are compared to the given value using the
    // Object.Equals method.
    //
    // This method uses the Array.IndexOf method to perform the
    // search.
    //
    public int IndexOf(T item, int index, int count)
    {
        if (index > _size) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessOrEqualException();

        if (count < 0 || index > _size - count) ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_Count();

        return Array.IndexOf(_items, item, index, count);
    }

    // Inserts the elements of the given collection at a given index. If
    // required, the capacity of the list is increased to twice the previous
    // capacity or the new size, whichever is larger.  Ranges may be added
    // to the end of the list by setting index to the List's size.
    //
    public void InsertRange(int index, IEnumerable<T> collection)
    {
        if (collection == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.collection);

        if ((uint)index > (uint)_size) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessOrEqualException();

        if (collection is ICollection<T> c)
        {
            var count = c.Count;
            if (count > 0)
            {
                if (_items.Length - _size < count) Grow(_size + count);
                if (index < _size) Array.Copy(_items, index, _items, index + count, _size - index);

                // If we're inserting a List into itself, we want to be able to deal with that.
                if (this == c)
                {
                    // Copy first part of _items to insert location
                    Array.Copy(_items, 0, _items, index, index);

                    // Copy last part of _items back to inserted location
                    Array.Copy(_items, index + count, _items, index * 2, _size - index);
                }
                else c.CopyTo(_items, index);

                _size += count;
            }
        }
        else
            using (var en = collection.GetEnumerator())
                while (en.MoveNext())
                    Insert(index++, en.Current);

        _version++;
    }

    // Returns the index of the last occurrence of a given value in a range of
    // this list. The list is searched backwards, starting at the end
    // and ending at the first element in the list. The elements of the list
    // are compared to the given value using the Object.Equals method.
    //
    // This method uses the Array.LastIndexOf method to perform the
    // search.
    //
    public int LastIndexOf(T item)
    {
        if (_size == 0)

            // Special case for empty list
            return -1;

        return LastIndexOf(item, _size - 1, _size);
    }

    // Returns the index of the last occurrence of a given value in a range of
    // this list. The list is searched backwards, starting at index
    // index and ending at the first element in the list. The
    // elements of the list are compared to the given value using the
    // Object.Equals method.
    //
    // This method uses the Array.LastIndexOf method to perform the
    // search.
    //
    public int LastIndexOf(T item, int index)
    {
        if (index >= _size) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessException();
        return LastIndexOf(item, index, index + 1);
    }

    // Returns the index of the last occurrence of a given value in a range of
    // this list. The list is searched backwards, starting at index
    // index and upto count elements. The elements of
    // the list are compared to the given value using the Object.Equals
    // method.
    //
    // This method uses the Array.LastIndexOf method to perform the
    // search.
    //
    public int LastIndexOf(T item, int index, int count)
    {
        if (Count != 0 && index < 0) ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();

        if (Count != 0 && count < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count,
                ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

        if (_size == 0)

            // Special case for empty list
            return -1;

        if (index >= _size)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index,
                ExceptionResource.ArgumentOutOfRange_BiggerThanCollection);

        if (count > index + 1)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count,
                ExceptionResource.ArgumentOutOfRange_BiggerThanCollection);

        return Array.LastIndexOf(_items, item, index, count);
    }

    // This method removes all items which matches the predicate.
    // The complexity is O(n).
    public int RemoveAll(Predicate<T> match)
    {
        if (match == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);

        var freeIndex = 0; // the first free slot in items array
        var items = _items;

        // Find the first item which needs to be removed.
        while (freeIndex < _size && !match(items[freeIndex])) freeIndex++;
        if (freeIndex >= _size) return 0;

        var current = freeIndex + 1;
        while (current < _size)
        {
            // Find the first item which needs to be kept.
            while (current < _size && match(items[current])) current++;

            if (current < _size)

                // copy item to the free slot.
                items[freeIndex++] = items[current++];
        }

        if (s_clearItems)
            Array.Clear(items,
                freeIndex,
                _size - freeIndex); // Clear the elements so that the gc can reclaim the references.

        var result = _size - freeIndex;
        _size = freeIndex;
        _version++;
        return result;
    }

    // Removes a range of elements from this list.
    public void RemoveRange(int index, int count)
    {
        if (index < 0) ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();

        if (count < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count,
                ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

        if (_size - index < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        if (count > 0)
        {
            _size -= count;
            if (index < _size) Array.Copy(_items, index + count, _items, index, _size - index);

            _version++;
            if (s_clearItems) Array.Clear(_items, _size, count);
        }
    }

    // Reverses the elements in this list.
    public void Reverse() => Reverse(0, Count);

    // Reverses the elements in a range of this list. Following a call to this
    // method, an element in the range given by index and count
    // which was previously located at index i will now be located at
    // index index + (index + count - i - 1).
    //
    public void Reverse(int index, int count)
    {
        if (index < 0) ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();

        if (count < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count,
                ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

        if (_size - index < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        if (count > 1) Array.Reverse(_items, index, count);
        _version++;
    }

    // Sorts the elements in this list.  Uses the default comparer and
    // Array.Sort.
    public void Sort() => Sort(0, Count, null);

    // Sorts the elements in this list.  Uses Array.Sort with the
    // provided comparer.
    public void Sort(IComparer<T>? comparer) => Sort(0, Count, comparer);

    // Sorts the elements in a section of this list. The sort compares the
    // elements to each other using the given IComparer interface. If
    // comparer is null, the elements are compared to each other using
    // the IComparable interface, which in that case must be implemented by all
    // elements of the list.
    //
    // This method uses the Array.Sort method to sort the elements.
    //
    public void Sort(int index, int count, IComparer<T>? comparer)
    {
        if (index < 0) ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();

        if (count < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count,
                ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

        if (_size - index < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        if (count > 1) Array.Sort(_items, index, count, comparer);
        _version++;
    }

    public void Sort(Comparison<T> comparison)
    {
        if (comparison == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.comparison);

        if (_size > 1) Array.Sort(_items, 0, _size, new Comparer(comparison));
        _version++;
    }

    // ToArray returns an array containing the contents of the List.
    // This requires copying the List, which is an O(n) operation.
    public T[] ToArray()
    {
        if (_size == 0) return s_emptyArray;

        return _items.AsSpan(0, _size).ToArray();
    }

    // Sets the capacity of this list to the size of the list. This method can
    // be used to minimize a list's memory overhead once it is known that no
    // new elements will be added to the list. To completely clear a list and
    // release all memory referenced by the list, execute the following
    // statements:
    //
    // list.Clear();
    // list.TrimExcess();
    //
    public void TrimExcess()
    {
        var threshold = (int)(_items.Length * 0.9);
        if (_size < threshold) Capacity = _size;
    }

    public bool TrueForAll(Predicate<T> match)
    {
        if (match == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);

        var items = _items;

        for (var i = 0; i < _size; i++)
            if (!match(items[i]))
                return false;

        return true;
    }

    /// <summary>
    ///     Advances the <see cref="Count"/> by the number of items specified, increasing the capacity if required, then
    ///     returns a <see cref="Span{T}"/> representing the set of items to be added, allowing direct writes to that section of the
    ///     collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<T> GetInsertSpan(int index, int count) => GetInsertSpan(index, count, true);

    internal Span<T> GetInsertSpan(int index, int count, bool clearSpan)
    {
        EnsureCapacity(_size + count);

        if (index < _size) Array.Copy(_items, index, _items, index + count, _size - index);

        _size += count;
        _version++;

        var output = _items.AsSpan(index, count);

        if (clearSpan && s_clearItems) output.Clear();

        return output;
    }

    public void InsertRange(int index, T[] array)
    {
        if (array == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);

        InsertRange(index, array.AsSpan());
    }

    public void InsertRange(int index, ReadOnlySpan<T> span)
    {
        if ((uint)index > (uint)_size) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessOrEqualException();

        var newSpan = GetInsertSpan(index, span.Length, false);
        span.CopyTo(newSpan);
    }

    /// <summary>
    ///     Adds the elements of the given array to the end of this list. If required, the capacity of the list is increased to
    ///     twice the previous capacity or the new size, whichever is larger.
    /// </summary>
    public void AddRange(T[] array)
    {
        if (array == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);

        AddRange(array.AsSpan());
    }

    /// <summary>
    ///     Adds the elements of the given <see cref="ReadOnlySpan{T}"/> to the end of this list. If required, the capacity of
    ///     the list is increased to twice the previous capacity or the new size, whichever is larger.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddRange(ReadOnlySpan<T> span) => span.CopyTo(GetInsertSpan(_size, span.Length, false));

    /// <summary>Copies this List into the given span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in Span<T> dest) => CopyTo(0, dest, 0, _size);

    /// <summary>Copies this List into the given span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in Span<T> dest, int destIndex) => CopyTo(0, dest, destIndex, _size);

    /// <summary>Copies this List into the given span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in Span<T> dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public void CopyTo(int index, in Span<T> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        if (count < 0) ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_NeedNonNegNum();

        if (dest.Length - destIndex < count || _size - index < count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        var src = _items.AsSpan(0, _size);

        if (src.Length == 0) return;

        src.Slice(index, count).CopyTo(dest.Slice(destIndex, count));
    }

    public void ConvertAll<TOut>(PooledList<TOut> output, Converter<T, TOut> converter)
    {
        if (converter == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.converter);

        if (output == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.output);

        var items = _items;

        for (var i = 0; i < _size; i++) output.Add(converter(items[i]));
    }

    public void FindAll(PooledList<T> output, Predicate<T> match)
    {
        if (match == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);

        if (output == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.output);

        var items = _items;

        for (var i = 0; i < _size; i++)
            if (match(items[i]))
                output.Add(items[i]);
    }

    public bool TryFind(Predicate<T> match, out T result)
    {
        if (match == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);

        var items = _items;

        for (var i = 0; i < _size; i++)
            if (match(items[i]))
            {
                result = items[i];
                return true;
            }

        result = default;
        return false;
    }

    public bool TryFindLast(Predicate<T> match, out T result)
    {
        if (match is null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);

        var items = _items;

        for (var i = _size - 1; i >= 0; i--)
            if (match(items[i]))
            {
                result = items[i];
                return true;
            }

        result = default;
        return false;
    }

    void ReturnArray(T[] replaceWith)
    {
        if (_items.IsNullOrEmpty() == false)
            try
            {
                _pool.Return(_items, s_clearItems);
            }
            catch { }

        _items = replaceWith ?? s_emptyArray;
    }

    public struct Enumerator : IEnumerator<T>, IEnumerator
    {
        readonly PooledList<T> _list;
        int _index;
        readonly int _version;

        public Enumerator(PooledList<T> list)
        {
            _list = list;
            _index = 0;
            _version = list._version;
            Current = default;
        }

        public void Dispose() { }

        public bool MoveNext()
        {
            var localList = _list;

            if (_version == localList._version && (uint)_index < (uint)localList._size)
            {
                Current = localList._items[_index];
                _index++;
                return true;
            }

            return MoveNextRare();
        }

        bool MoveNextRare()
        {
            if (_version != _list._version) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            _index = _list._size + 1;
            Current = default;
            return false;
        }

        public T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            private set;
        }

        object? IEnumerator.Current
        {
            get
            {
                if (_index == 0 || _index == _list._size + 1)
                    ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();

                return Current;
            }
        }

        void IEnumerator.Reset()
        {
            if (_version != _list._version) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            _index = 0;
            Current = default;
        }
    }

    readonly struct Comparer : IComparer<T>
    {
        readonly Comparison<T> _comparison;

        public Comparer(Comparison<T> comparison) => _comparison = comparison;

        public int Compare(T x, T y) => _comparison(x, y);
    }
}