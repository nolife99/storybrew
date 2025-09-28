// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/Queue.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic;

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

public sealed class PooledQueue<T> : IReadOnlyCollection<T>
{
    static readonly T[] s_emptyArray = [];

    internal static readonly bool s_clearArray = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    internal readonly ArrayPool<T> _pool;
    internal T[] _array;
    internal int _head;

    internal int _size;
    internal int _tail;
    internal int _version;

    public PooledQueue() : this(ArrayPool<T>.Shared) { }

    public PooledQueue(int capacity) : this(capacity, ArrayPool<T>.Shared) { }

    public PooledQueue(IEnumerable<T> collection) : this(collection, ArrayPool<T>.Shared) { }

    public PooledQueue(ArrayPool<T> pool)
    {
        _pool = pool ?? ArrayPool<T>.Shared;
        _array = s_emptyArray;
    }

    public PooledQueue(int capacity, ArrayPool<T> pool)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        _pool = pool ?? ArrayPool<T>.Shared;
        _array = capacity == 0 ? s_emptyArray : _pool.Rent(capacity);
    }

    public PooledQueue(IEnumerable<T> collection, ArrayPool<T> pool)
    {
        ArgumentNullException.ThrowIfNull(collection);

        _pool = pool ?? ArrayPool<T>.Shared;
        _array = EnumerableHelpers.ToArray(collection, s_emptyArray, _pool, out _size);
        if (_size != _array.Length) _tail = _size;
    }

    public PooledQueue(T[] items) : this(items.AsSpan(), ArrayPool<T>.Shared) { }

    public PooledQueue(T[] items, ArrayPool<T> pool) : this(items.AsSpan(), pool) { }

    public PooledQueue(ReadOnlySpan<T> span) : this(span, ArrayPool<T>.Shared) { }

    public PooledQueue(ReadOnlySpan<T> span, ArrayPool<T> pool)
    {
        _pool = pool ?? ArrayPool<T>.Shared;

        var count = span.Length;

        if (count == 0) _array = s_emptyArray;
        else
        {
            _array = _pool.Rent(count);
            span.CopyTo(_array);
            _size = count;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _size;
    }

    public void Clear()
    {
        if (_size != 0)
        {
            if (s_clearArray)
            {
                if (_head < _tail) Array.Clear(_array, _head, _size);
                else
                {
                    Array.Clear(_array, _head, _array.Length - _head);
                    Array.Clear(_array, 0, _tail);
                }
            }

            _size = 0;
        }

        _head = 0;
        _tail = 0;
        _version++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest) => CopyTo(dest, 0, _size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest, int destIndex) => CopyTo(dest, destIndex, _size);

    public void CopyTo(T[] dest, int destIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(dest);

        CopyTo(dest.AsSpan(destIndex, count));
    }

    public void Enqueue(T item)
    {
        if (_size == _array.Length) Grow(_size + 1);

        _array[_tail] = item;
        MoveNext(ref _tail);
        _size++;
        _version++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(this);

    public T Dequeue()
    {
        var head = _head;
        var array = _array;

        if (_size == 0) ThrowForEmptyQueue();

        var removed = array[head];
        if (s_clearArray) array[head] = default!;
        MoveNext(ref _head);
        _size--;
        _version++;
        return removed;
    }

    public bool TryDequeue([MaybeNullWhen(false)] out T result)
    {
        var head = _head;
        var array = _array;

        if (_size == 0)
        {
            result = default!;
            return false;
        }

        result = array[head];
        if (s_clearArray) array[head] = default!;
        MoveNext(ref _head);
        _size--;
        _version++;
        return true;
    }

    public T Peek()
    {
        if (_size == 0) ThrowForEmptyQueue();

        return _array[_head];
    }

    public bool TryPeek([MaybeNullWhen(false)] out T result)
    {
        if (_size == 0)
        {
            result = default!;
            return false;
        }

        result = _array[_head];
        return true;
    }

    public bool Contains(T item)
        => _size == 0 ? false :
            _head < _tail ? Array.IndexOf(_array, item, _head, _size) >= 0 :
            Array.IndexOf(_array, item, _head, _array.Length - _head) >= 0 ||
            Array.IndexOf(_array, item, 0, _tail) >= 0;

    public T[] ToArray()
    {
        if (_size == 0) return s_emptyArray;

        var arr = new T[_size];

        if (_head < _tail) Array.Copy(_array, _head, arr, 0, _size);
        else
        {
            Array.Copy(_array, _head, arr, 0, _array.Length - _head);
            Array.Copy(_array, 0, arr, _array.Length - _head, _tail);
        }

        return arr;
    }

    void SetCapacity(int capacity)
    {
        var newArray = _pool.Rent(capacity);

        if (capacity < _array.Length && newArray.Length >= _array.Length)
        {
            _pool.Return(newArray);
            return;
        }

        if (_size > 0)
        {
            if (_head < _tail) Array.Copy(_array, _head, newArray, 0, _size);
            else
            {
                Array.Copy(_array, _head, newArray, 0, _array.Length - _head);
                Array.Copy(_array, 0, newArray, _array.Length - _head, _tail);
            }
        }

        ReturnArray(newArray);
        _head = 0;
        _tail = _size == capacity ? 0 : _size;
        _version++;
    }

    void MoveNext(ref int index)
    {
        var tmp = index + 1;
        if (tmp == _array.Length) tmp = 0;
        index = tmp;
    }

    static void ThrowForEmptyQueue() => ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EmptyQueue();

    public void TrimExcess()
    {
        var threshold = (int)(_array.Length * 0.9);
        if (_size < threshold) SetCapacity(_size);
    }

    public int EnsureCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        if (_array.Length < capacity) Grow(capacity);

        return _array.Length;
    }

    void Grow(int capacity)
    {
        const int GrowFactor = 2;
        const int MinimumGrow = 4;

        var newcapacity = GrowFactor * _array.Length;

        if ((uint)newcapacity > Array.MaxLength) newcapacity = Array.MaxLength;

        newcapacity = Math.Max(newcapacity, _array.Length + MinimumGrow);

        if (newcapacity < capacity) newcapacity = capacity;

        SetCapacity(newcapacity);
    }

    void ReturnArray(T[] replaceWith)
    {
        if (_array is not null) _pool.Return(_array, s_clearArray);

        _array = replaceWith ?? s_emptyArray;
    }

    public void Dispose()
    {
        ReturnArray(s_emptyArray);
        _head = _tail = _size = 0;
        _version++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped Span<T> dest) => CopyTo(dest, 0, _size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped Span<T> dest, int destIndex) => CopyTo(dest, destIndex, _size);

    public void CopyTo(scoped Span<T> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (dest.Length - destIndex < count || _size < count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        var numToCopy = count;
        var src = _array.AsSpan(0, _size);

        if (src.Length == 0 || numToCopy == 0) return;

        var firstPart = Math.Min(src.Length - _head, numToCopy);
        src.Slice(_head, firstPart).CopyTo(dest.Slice(destIndex, firstPart));

        numToCopy -= firstPart;
        if (numToCopy <= 0) return;

        destIndex += src.Length - _head;
        src[..numToCopy].CopyTo(dest.Slice(destIndex, numToCopy));
    }

    public struct Enumerator : IEnumerator<T>
    {
        readonly PooledQueue<T> _q;
        readonly int _version;
        int _index;
        T _currentElement;

        internal Enumerator(PooledQueue<T> q)
        {
            _q = q;
            _version = q._version;
            _index = -1;
            _currentElement = default;
        }

        public void Dispose()
        {
            _index = -2;
            _currentElement = default;
        }

        public bool MoveNext()
        {
            if (_version != _q._version)
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            if (_index == -2) return false;

            _index++;

            if (_index == _q._size)
            {
                _index = -2;
                _currentElement = default;
                return false;
            }

            var array = _q._array;
            var capacity = array.Length;

            var arrayIndex = _q._head + _index;
            if (arrayIndex >= capacity) arrayIndex -= capacity;

            _currentElement = array[arrayIndex];
            return true;
        }

        public T Current
        {
            get
            {
                if (_index < 0) ThrowEnumerationNotStartedOrEnded();
                return _currentElement!;
            }
        }

        void ThrowEnumerationNotStartedOrEnded()
        {
            if (_index == -1) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumNotStarted();
            else ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumEnded();
        }

        object IEnumerator.Current => Current;

        void IEnumerator.Reset()
        {
            if (_version != _q._version)
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            _index = -1;
            _currentElement = default;
        }
    }
}