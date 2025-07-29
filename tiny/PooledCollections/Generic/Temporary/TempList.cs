// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/List.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public ref struct TempList<T>
{
    const int DefaultCapacity = 4;

    internal T[] _items;
    internal int _size;
    internal int _version;

    internal readonly ArrayPool<T> _pool;

    static readonly T[] s_emptyArray = [];

    internal static readonly bool s_clearItems = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    internal TempList(int capacity, ArrayPool<T> pool)
    {
        _pool = pool ?? ArrayPool<T>.Shared;
        if (capacity <= 0) _items = s_emptyArray;
        else
        {
            _items = _pool.Rent(capacity);
            _ref = ref MemoryMarshal.GetArrayDataReference(_items);
        }

        _size = 0;
        _version = 0;
    }

    internal TempList(IEnumerable<T> collection, ArrayPool<T> pool)
    {
        _pool = pool ?? ArrayPool<T>.Shared;
        _version = 0;

        switch (collection)
        {
            case ICollection<T> c:
                var count = c.Count;
                if (count == 0)
                {
                    _items = s_emptyArray;
                    _size = 0;
                }
                else
                {
                    _items = _pool.Rent(count);
                    c.CopyTo(_items, 0);
                    _size = count;

                    _ref = ref MemoryMarshal.GetArrayDataReference(_items);
                }

                break;

            case string s:
                count = s.Length;
                if (count == 0)
                {
                    _items = s_emptyArray;
                    _size = 0;
                }
                else
                {
                    _items = _pool.Rent(count);
                    s.CopyTo(0, Unsafe.As<char[]>(_items), 0, count);
                    _size = count;

                    _ref = ref MemoryMarshal.GetArrayDataReference(_items);
                }

                break;

            default:
                _size = 0;
                _items = s_emptyArray;

                if (collection is null) return;

                using (var en = collection!.GetEnumerator())
                    while (en.MoveNext())
                        Add(en.Current);

                _ref = ref MemoryMarshal.GetArrayDataReference(_items);
                break;
        }
    }

    public int Capacity
    {
        get => _items.Length;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, _size);

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

            if (_size > 0) this.AsReadOnlySpan().CopyTo(newItems);

            ReturnArray(newItems);
        }
    }

    public readonly int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _size;
    }

    public readonly bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items is not null;
    }

    internal ref T _ref;

    public ref T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_size) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessException();
            return ref Unsafe.Add(ref _ref, index);
        }
    }

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
        else
        {
            Grow(size + 1);
            _size = size + 1;
            _items[size] = item;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddRange(IEnumerable<T> collection) => InsertRange(_size, collection);

    public readonly int BinarySearch(int index, int count, T item, IComparer<T> comparer)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (_size - index < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        return Array.BinarySearch(_items, index, count, item, comparer);
    }

    public readonly int BinarySearch(T item) => BinarySearch(0, Count, item, null);

    public readonly int BinarySearch(T item, IComparer<T> comparer) => BinarySearch(0, Count, item, comparer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    public readonly bool Contains(T item) => _size != 0 && IndexOf(item) >= 0;

    public readonly TempList<TOut> ConvertAll<TOut>(Converter<T, TOut> converter, ArrayPool<TOut> pool = null)
    {
        ArgumentNullException.ThrowIfNull(converter);

        TempList<TOut> list = new(_size, pool ?? ArrayPool<TOut>.Shared);
        var src = _items;
        var dst = list._items;

        for (var i = 0; i < _size; i++) dst[i] = converter(src[i]);
        list._size = _size;
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(T[] dest) => CopyTo(0, dest, 0, _size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(T[] dest, int destIndex) => CopyTo(0, dest, destIndex, _size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(T[] dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public readonly void CopyTo(int index, T[] dest, int destIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(dest);

        CopyTo(index, new Span<T>(dest), destIndex, count);
    }

    public int EnsureCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        if (_items.Length < capacity)
        {
            Grow(capacity);
            _version++;
        }

        return _items.Length;
    }

    void Grow(int capacity)
    {
        var newcapacity = _items.Length == 0 ? DefaultCapacity : 2 * _items.Length;
        if ((uint)newcapacity > Array.MaxLength) newcapacity = Array.MaxLength;
        if (newcapacity < capacity) newcapacity = capacity;

        Capacity = newcapacity;
    }

    public readonly bool Exists(Predicate<T> match) => FindIndex(match) != -1;

    public readonly T Find(Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var items = _items;

        for (var i = 0; i < _size; i++)
            if (match(items[i]))
                return items[i];

        return default;
    }

    public readonly TempList<T> FindAll(Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var list = new TempList<T>();
        var items = _items;

        for (var i = 0; i < _size; i++)
            if (match(items[i]))
                list.Add(items[i]);

        return list;
    }

    public readonly int FindIndex(Predicate<T> match) => FindIndex(0, _size, match);

    public readonly int FindIndex(int startIndex, Predicate<T> match) => FindIndex(startIndex, _size - startIndex, match);

    public readonly int FindIndex(int startIndex, int count, Predicate<T> match)
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

    public readonly T FindLast(Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var items = _items;

        for (var i = _size - 1; i >= 0; i--)
            if (match(items[i]))
                return items[i];

        return default;
    }

    public readonly int FindLastIndex(Predicate<T> match) => FindLastIndex(_size - 1, _size, match);

    public readonly int FindLastIndex(int startIndex, Predicate<T> match)
        => FindLastIndex(startIndex, startIndex + 1, match);

    public readonly int FindLastIndex(int startIndex, int count, Predicate<T> match)
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

    public readonly void ForEach(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var version = _version;
        var items = _items;

        for (var i = 0; i < _size; i++)
        {
            if (version != _version) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            action(items[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Enumerator GetEnumerator() => new(in this);

    public readonly TempList<T> GetRange(int index, int count, ArrayPool<T> pool = null)
    {
        var copySpan = this.AsReadOnlySpan().Slice(index, count);
        var list = new TempList<T>(copySpan, pool ?? ArrayPool<T>.Shared);
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int IndexOf(T item) => Array.IndexOf(_items, item, 0, _size);

    public readonly int IndexOf(T item, int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _size);
        return Array.IndexOf(_items, item, index, _size - index);
    }

    public readonly int IndexOf(T item, int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _size);

        if (count < 0 || index > _size - count) ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_Count();

        return Array.IndexOf(_items, item, index, count);
    }

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

                c.CopyTo(_items, index);
                _size += count;
            }
        }
        else
            using (var en = collection.GetEnumerator())
                while (en.MoveNext())
                    Insert(index++, en.Current);

        _version++;
    }

    public void InsertRange(int index, TempList<T> collection)
    {
        var count = collection.Count;
        if (_items == collection._items)
        {
            Array.Copy(_items, 0, _items, index, index);
            Array.Copy(_items, index + count, _items, index * 2, _size - index);
        }
        else if (count > 0)
        {
            if (_items.Length - _size < count) Grow(_size + count);
            if (index < _size) Array.Copy(_items, index, _items, index + count, _size - index);

            collection.CopyTo(_items, index);
            _size += count;
        }
    }

    public readonly int LastIndexOf(T item)
    {
        if (_size == 0) return -1;

        return LastIndexOf(item, _size - 1, _size);
    }

    public readonly int LastIndexOf(T item, int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _size);
        return LastIndexOf(item, index, index + 1);
    }

    public readonly int LastIndexOf(T item, int index, int count)
    {
        if (Count != 0 && index < 0) ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();

        if (Count != 0 && count < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count,
                ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

        if (_size == 0) return -1;

        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _size);

        if (count > index + 1)
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count,
                ExceptionResource.ArgumentOutOfRange_BiggerThanCollection);

        return Array.LastIndexOf(_items, item, index, count);
    }

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

        if (s_clearItems) Array.Clear(_items, freeIndex, _size - freeIndex);

        var result = _size - freeIndex;
        _size = freeIndex;
        _version++;
        return result;
    }

    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_size) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessException();
        _size--;
        if (index < _size) Array.Copy(_items, index + 1, _items, index, _size - index);
        if (s_clearItems) _items[_size] = default!;
        _version++;
    }

    public void RemoveRange(int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (_size - index < count) ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        if (count <= 0) return;

        _size -= count;
        if (index < _size) Array.Copy(_items, index + count, _items, index, _size - index);

        ++_version;
        if (s_clearItems) Array.Clear(_items, _size, count);
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

        if (count > 1) this.AsSpan().Sort(comparer);
        _version++;
    }

    public void Sort(Comparison<T> comparison)
    {
        if (_size > 1) this.AsSpan().Sort(comparison);
        _version++;
    }

    public T[] ToArray() => _size == 0 ? s_emptyArray : _items.AsSpan(0, _size).ToArray();

    public void TrimExcess()
    {
        if (_size < _items.Length * 0.9) Capacity = _size;
    }

    public readonly bool TrueForAll(Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        for (var i = 0; i < _size; i++)
            if (!match(_items[i]))
                return false;

        return true;
    }

    public ref struct Enumerator : IEnumerator<T>
    {
        readonly TempList<T> _list;
        int _index;
        readonly int _version;

        internal Enumerator(scoped ref readonly TempList<T> list)
        {
            _list = list;
            _index = -1;
            _version = list._version;
        }

        public bool MoveNext()
        {
            if (_version != _list._version) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            var index = _index + 1;
            if (index >= _list._size) return false;

            _index = index;
            return true;
        }

        public void Reset() => _index = -1;

        public ref T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _list._items[_index];
        }

        T IEnumerator<T>.Current => Current;
        object IEnumerator.Current => Current;
        void IDisposable.Dispose() { }
    }

    internal TempList(scoped ReadOnlySpan<T> span, ArrayPool<T> pool)
    {
        _pool = pool ?? ArrayPool<T>.Shared;

        var count = span.Length;

        if (count == 0)
        {
            _items = s_emptyArray;
            _size = 0;
        }
        else
        {
            _items = _pool.Rent(count);
            span.CopyTo(_items);

            _ref = ref MemoryMarshal.GetArrayDataReference(_items);
            _size = count;
        }

        _version = 0;
    }

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
        ArgumentNullException.ThrowIfNull(array);

        InsertRange(index, new ReadOnlySpan<T>(array));
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

        AddRange(new ReadOnlySpan<T>(array));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining), OverloadResolutionPriority(1)]
    public void AddRange(scoped ReadOnlySpan<T> span) => span.CopyTo(GetInsertSpan(_size, span.Length, false));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(scoped Span<T> dest) => CopyTo(0, dest, 0, _size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(scoped Span<T> dest, int destIndex) => CopyTo(0, dest, destIndex, _size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(scoped Span<T> dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public readonly void CopyTo(int index, scoped Span<T> dest, int destIndex, int count)
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

    public readonly void ConvertAll<TOut>(scoped ref TempList<TOut> output, Converter<T, TOut> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);

        var items = _items;

        for (var i = 0; i < _size; i++) output.Add(converter(items[i]));
    }

    public readonly void FindAll(scoped ref TempList<T> output, Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var items = _items;

        for (var i = 0; i < _size; i++)
            if (match(items[i]))
                output.Add(items[i]);
    }

    public readonly bool TryFind(Predicate<T> match, out T result)
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

    public readonly bool TryFindLast(Predicate<T> match, out T result)
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
        if (IsValid) _pool.Return(_items, s_clearItems);

        _items = replaceWith ?? s_emptyArray;
        _ref = ref MemoryMarshal.GetArrayDataReference(_items);
    }

    public void Dispose()
    {
        ReturnArray(s_emptyArray);
        _size = 0;
        _version++;
    }
}