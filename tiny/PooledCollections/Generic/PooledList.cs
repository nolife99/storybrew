// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/List.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic;

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tiny.PooledCollections.Generic.Internals;

public sealed class PooledList<T> : IList<T>, IReadOnlyList<T>, IDisposable
{
    const int DefaultCapacity = 4;

    static readonly T[] s_emptyArray = [];

    internal static readonly bool s_clearItems = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    internal readonly ArrayPool<T> _pool;

    internal T[] _items;

    internal int _size;
    internal int _version;

    public PooledList() : this(ArrayPool<T>.Shared) { }

    public PooledList(int capacity) : this(capacity, ArrayPool<T>.Shared) { }

    public PooledList(IEnumerable<T> collection) : this(collection, ArrayPool<T>.Shared) { }

    public PooledList(ArrayPool<T> pool)
    {
        _items = s_emptyArray;
        _pool = pool ?? ArrayPool<T>.Shared;
    }

    public PooledList(int capacity, ArrayPool<T> pool)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        _pool = pool ?? ArrayPool<T>.Shared;
        _items = capacity == 0 ? s_emptyArray : _pool.Rent(capacity);
    }

    public PooledList(IEnumerable<T> collection, ArrayPool<T> pool)
    {
        ArgumentNullException.ThrowIfNull(collection);

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

    public PooledList(scoped ReadOnlySpan<T> span) : this(span, ArrayPool<T>.Shared) { }

    public PooledList(scoped ReadOnlySpan<T> span, ArrayPool<T> pool)
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

    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    public void Dispose()
    {
        ReturnArray(s_emptyArray);
        _size = 0;
        _version++;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _size;
    }

    bool ICollection<T>.IsReadOnly => false;

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_size) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessException();
            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_items), index);
        }
        set
        {
            if ((uint)index >= (uint)_size) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessException();
            Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_items), index) = value;
            _version++;
        }
    }

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
        else
        {
            Grow(size + 1);
            _size = size + 1;
            _items[size] = item;
        }
    }

    public void Clear()
    {
        _version++;
        if (s_clearItems)
        {
            var size = _size;
            _size = 0;
            if (size > 0) Array.Clear(_items, 0, size);
        }
        else _size = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item) => _size != 0 && IndexOf(item) >= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] array, int arrayIndex) => CopyTo(0, array, arrayIndex, _size);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);
    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(T item) => Array.IndexOf(_items, item, 0, _size);

    public void Insert(int index, T item)
    {
        if ((uint)index > (uint)_size)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index,
                ExceptionResource.ArgumentOutOfRange_ListInsert);

        if (_size == _items.Length) Grow(_size + 1);
        if (index < _size) Array.Copy(_items, index, _items, index + 1, _size - index);
        _items[index] = item;
        _size++;
        _version++;
    }

    public bool Remove(T item)
    {
        var index = IndexOf(item);
        if (index < 0) return false;

        RemoveAt(index);
        return true;
    }

    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_size) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessException();
        _size--;
        if (index < _size) Array.Copy(_items, index + 1, _items, index, _size - index);
        if (s_clearItems) _items[_size] = default!;
        _version++;
    }

    public void AddRange(IEnumerable<T> collection) => InsertRange(_size, collection);

    public ReadOnlyCollection<T> AsReadOnly() => new(this);

    public int BinarySearch(int index, int count, T item, IComparer<T> comparer)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (_size - index < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        return Array.BinarySearch(_items, index, count, item, comparer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int BinarySearch(T item) => BinarySearch(0, Count, item, null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int BinarySearch(T item, IComparer<T> comparer) => BinarySearch(0, Count, item, comparer);

    public PooledList<TOut> ConvertAll<TOut>(Converter<T, TOut> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);

        var list = new PooledList<TOut>(_size);
        var src = _items;
        var dst = list._items;

        for (var i = 0; i < _size; i++) dst[i] = converter(src[i]);
        list._size = _size;
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest) => CopyTo(0, dest, 0, _size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public void CopyTo(int index, T[] dest, int destIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(dest);

        CopyTo(index, dest.AsSpan(), destIndex, count);
    }

    public int EnsureCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        if (_items.Length >= capacity) return _items.Length;

        Grow(capacity);
        _version++;

        return _items.Length;
    }

    void Grow(int capacity)
    {
        var newcapacity = _items.Length == 0 ? DefaultCapacity : 2 * _items.Length;

        if ((uint)newcapacity > Array.MaxLength) newcapacity = Array.MaxLength;
        if (newcapacity < capacity) newcapacity = capacity;

        Capacity = newcapacity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Exists(Predicate<T> match) => FindIndex(match) != -1;

    public T Find(Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var items = _items;

        for (var i = 0; i < _size; i++)
            if (match(items[i]))
                return items[i];

        return default;
    }

    public PooledList<T> FindAll(Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var list = new PooledList<T>();
        var items = _items;

        for (var i = 0; i < _size; i++)
            if (match(items[i]))
                list.Add(items[i]);

        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindIndex(Predicate<T> match) => FindIndex(0, _size, match);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindIndex(int startIndex, Predicate<T> match) => FindIndex(startIndex, _size - startIndex, match);

    public int FindIndex(int startIndex, int count, Predicate<T> match)
    {
        if ((uint)startIndex > (uint)_size)
            ThrowHelper.ThrowStartIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        if (count < 0 || startIndex > _size - count) ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_Count();

        ArgumentNullException.ThrowIfNull(match);

        var endIndex = startIndex + count;
        var items = _items;

        for (var i = startIndex; i < endIndex; i++)
            if (match(items[i]))
                return i;

        return -1;
    }

    public T FindLast(Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var items = _items;

        for (var i = _size - 1; i >= 0; i--)
            if (match(items[i]))
                return items[i];

        return default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindLastIndex(Predicate<T> match) => FindLastIndex(_size - 1, _size, match);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindLastIndex(int startIndex, Predicate<T> match) => FindLastIndex(startIndex, startIndex + 1, match);

    public int FindLastIndex(int startIndex, int count, Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (_size == 0)
        {
            if (startIndex != -1) ThrowHelper.ThrowStartIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLess();
        }
        else
        {
            if ((uint)startIndex >= (uint)_size)
                ThrowHelper.ThrowStartIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLess();
        }

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
        ArgumentNullException.ThrowIfNull(action);

        var version = _version;
        var items = _items;

        for (var i = 0; i < _size; i++)
        {
            if (version != _version) break;

            action(items[i]);
        }

        if (version != _version) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(this);

    public PooledList<T> GetRange(int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (_size - index < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        var list = new PooledList<T>(count);
        Array.Copy(_items, index, list._items, 0, count);
        list._size = count;
        return list;
    }

    public int IndexOf(T item, int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _size);
        return Array.IndexOf(_items, item, index, _size - index);
    }

    public int IndexOf(T item, int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _size);

        if (count < 0 || index > _size - count) ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_Count();

        return Array.IndexOf(_items, item, index, count);
    }

    public void InsertRange(int index, IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        if ((uint)index > (uint)_size) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessOrEqualException();

        if (collection is ICollection<T> c)
        {
            var count = c.Count;
            if (count > 0)
            {
                if (_items.Length - _size < count) Grow(_size + count);
                if (index < _size) Array.Copy(_items, index, _items, index + count, _size - index);

                if (ReferenceEquals(this, c))
                {
                    Array.Copy(_items, 0, _items, index, index);
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

    public int LastIndexOf(T item)
    {
        if (_size == 0) return -1;

        return LastIndexOf(item, _size - 1, _size);
    }

    public int LastIndexOf(T item, int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _size);
        return LastIndexOf(item, index, index + 1);
    }

    public int LastIndexOf(T item, int index, int count)
    {
        if (Count != 0 && index < 0) ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();

        if (Count != 0 && count < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count,
                ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

        if (_size == 0) return -1;

        if (index >= _size)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index,
                ExceptionResource.ArgumentOutOfRange_BiggerThanCollection);

        if (count > index + 1)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count,
                ExceptionResource.ArgumentOutOfRange_BiggerThanCollection);

        return Array.LastIndexOf(_items, item, index, count);
    }

    public int RemoveAll(Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var freeIndex = 0;
        var items = _items;

        while (freeIndex < _size && !match(items[freeIndex])) freeIndex++;
        if (freeIndex >= _size) return 0;

        var current = freeIndex + 1;
        while (current < _size)
        {
            while (current < _size && match(items[current])) current++;

            if (current < _size) items[freeIndex++] = items[current++];
        }

        if (s_clearItems) Array.Clear(items, freeIndex, _size - freeIndex);

        var result = _size - freeIndex;
        _size = freeIndex;
        _version++;
        return result;
    }

    public void RemoveRange(int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (_size - index < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        if (count > 0)
        {
            _size -= count;
            if (index < _size) Array.Copy(_items, index + count, _items, index, _size - index);

            _version++;
            if (s_clearItems) Array.Clear(_items, _size, count);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reverse() => Reverse(0, Count);

    public void Reverse(int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (_size - index < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        if (count > 1) Array.Reverse(_items, index, count);
        _version++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sort() => Sort(0, Count, null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sort(IComparer<T> comparer) => Sort(0, Count, comparer);

    public void Sort(int index, int count, IComparer<T> comparer)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (_size - index < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        if (count > 1) Array.Sort(_items, index, count, comparer);
        _version++;
    }

    public void Sort(Comparison<T> comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        if (_size > 1) this.AsSpan().Sort(comparison);
        _version++;
    }

    public T[] ToArray() => _size == 0 ? s_emptyArray : _items.AsSpan(0, _size).ToArray();

    public void TrimExcess()
    {
        var threshold = (int)(_items.Length * 0.9);
        if (_size < threshold) Capacity = _size;
    }

    public bool TrueForAll(Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var items = _items;

        for (var i = 0; i < _size; i++)
            if (!match(items[i]))
                return false;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<T> GetInsertSpan(int index, int count) => GetInsertSpan(index, count, true);

    internal Span<T> GetInsertSpan(int index, int count, bool clearSpan)
    {
        EnsureCapacity(_size + count);

        if (index < _size) Array.Copy(_items, index, _items, index + count, _size - index);

        _size += count;
        _version++;

        var output = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_items), index), count);

        if (clearSpan && s_clearItems) output.Clear();

        return output;
    }

    public void InsertRange(int index, T[] array)
    {
        ArgumentNullException.ThrowIfNull(array);

        InsertRange(index, array.AsSpan());
    }

    public void InsertRange(int index, scoped ReadOnlySpan<T> span)
    {
        if ((uint)index > (uint)_size) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessOrEqualException();

        var newSpan = GetInsertSpan(index, span.Length, false);
        span.CopyTo(newSpan);
    }

    public void AddRange(T[] array)
    {
        ArgumentNullException.ThrowIfNull(array);

        AddRange(array.AsSpan());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining), OverloadResolutionPriority(1)]
    public void AddRange(scoped ReadOnlySpan<T> span) => span.CopyTo(GetInsertSpan(_size, span.Length, false));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped Span<T> dest) => CopyTo(0, dest, 0, _size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped Span<T> dest, int destIndex) => CopyTo(0, dest, destIndex, _size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped Span<T> dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public void CopyTo(int index, scoped Span<T> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (dest.Length - destIndex < count || _size - index < count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        var src = _items.AsSpan(0, _size);

        if (src.Length == 0) return;

        src.Slice(index, count).CopyTo(dest.Slice(destIndex, count));
    }

    public void ConvertAll<TOut>(PooledList<TOut> output, Converter<T, TOut> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);

        ArgumentNullException.ThrowIfNull(output);

        var items = _items;

        for (var i = 0; i < _size; i++) output.Add(converter(items[i]));
    }

    public void FindAll(PooledList<T> output, Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        ArgumentNullException.ThrowIfNull(output);

        var items = _items;

        for (var i = 0; i < _size; i++)
            if (match(items[i]))
                output.Add(items[i]);
    }

    public bool TryFind(Predicate<T> match, out T result)
    {
        ArgumentNullException.ThrowIfNull(match);

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
        ArgumentNullException.ThrowIfNull(match);

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
        if (_items is not null) _pool.Return(_items, s_clearItems);

        _items = replaceWith ?? s_emptyArray;
    }

    public struct Enumerator : IEnumerator<T>
    {
        readonly PooledList<T> _list;
        int _index;
        readonly int _version;

        internal Enumerator(PooledList<T> list)
        {
            _list = list;
            _index = -1;
            _version = list._version;
        }

        public void Dispose() { }

        public bool MoveNext()
        {
            if (_version != _list._version) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            var index = _index + 1;
            if (index >= _list._size) return false;

            _index = index;
            return true;
        }

        public T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_list._items), _index);
        }

        object IEnumerator.Current
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

            _index = -1;
        }
    }
}