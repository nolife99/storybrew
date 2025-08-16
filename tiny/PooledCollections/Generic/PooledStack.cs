// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Collections/src/System/Collections/Generic/Stack.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic;

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

public sealed class PooledStack<T> : IReadOnlyCollection<T>
{
    const int DefaultCapacity = 4;

    static readonly T[] s_emptyArray = [];

    internal static readonly bool s_clearArray = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    internal readonly ArrayPool<T> _pool;
    internal T[] _array;

    internal int _size;
    internal int _version;

    public PooledStack() : this(ArrayPool<T>.Shared) { }

    public PooledStack(int capacity) : this(capacity, ArrayPool<T>.Shared) { }

    public PooledStack(IEnumerable<T> collection) : this(collection, ArrayPool<T>.Shared) { }

    public PooledStack(ArrayPool<T> pool)
    {
        _pool = pool ?? ArrayPool<T>.Shared;
        _array = s_emptyArray;
    }

    public PooledStack(int capacity, ArrayPool<T> pool)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        _pool = pool ?? ArrayPool<T>.Shared;
        _array = capacity == 0 ? s_emptyArray : _pool.Rent(capacity);
    }

    public PooledStack(IEnumerable<T> collection, ArrayPool<T> pool)
    {
        ArgumentNullException.ThrowIfNull(collection);

        _pool = pool ?? ArrayPool<T>.Shared;
        _array = EnumerableHelpers.ToArray(collection, s_emptyArray, _pool, out _size);
    }

    public PooledStack(T[] items) : this(items.AsSpan(), ArrayPool<T>.Shared) { }

    public PooledStack(T[] items, ArrayPool<T> pool) : this(items.AsSpan(), pool) { }

    public PooledStack(ReadOnlySpan<T> span) : this(span, ArrayPool<T>.Shared) { }

    public PooledStack(ReadOnlySpan<T> span, ArrayPool<T> pool)
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
        if (s_clearArray) Array.Clear(_array, 0, _size);

        _size = 0;
        _version++;
    }

    public bool Contains(T item) => _size != 0 && Array.LastIndexOf(_array, item, _size - 1) != -1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest) => CopyTo(dest, 0, _size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest, int destIndex) => CopyTo(dest, destIndex, _size);

    public void CopyTo(T[] dest, int destIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(dest);

        CopyTo(dest.AsSpan(), destIndex, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(this);

    public void TrimExcess()
    {
        if (_size == 0)
        {
            ReturnArray(s_emptyArray);
            _version++;
            return;
        }

        var threshold = (int)(_array.Length * 0.9);
        if (_size >= threshold) return;

        var newArray = _pool.Rent(_size);
        if (newArray.Length < _array.Length)
        {
            Array.Copy(_array, newArray, _size);
            ReturnArray(newArray);
            _version++;
        }
        else _pool.Return(newArray);
    }

    public T Peek()
    {
        var size = _size - 1;
        var array = _array;

        if ((uint)size >= (uint)array.Length) ThrowForEmptyStack();

        return array[size];
    }

    public bool TryPeek([MaybeNullWhen(false)] out T result)
    {
        var size = _size - 1;
        var array = _array;

        if ((uint)size >= (uint)array.Length)
        {
            result = default!;
            return false;
        }

        result = array[size];
        return true;
    }

    public T Pop()
    {
        var size = _size - 1;
        var array = _array;

        if ((uint)size >= (uint)array.Length) ThrowForEmptyStack();

        _version++;
        _size = size;
        var item = array[size];
        if (s_clearArray) array[size] = default!;
        return item;
    }

    public bool TryPop([MaybeNullWhen(false)] out T result)
    {
        var size = _size - 1;
        var array = _array;

        if ((uint)size >= (uint)array.Length)
        {
            result = default!;
            return false;
        }

        _version++;
        _size = size;
        result = array[size];
        if (s_clearArray) array[size] = default!;
        return true;
    }

    public void Push(T item)
    {
        var size = _size;
        var array = _array;

        if ((uint)size < (uint)array.Length)
        {
            array[size] = item;
            _version++;
            _size = size + 1;
        }
        else PushWithResize(item);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void PushWithResize(T item)
    {
        Grow(_size + 1);
        _array[_size] = item;
        _version++;
        _size++;
    }

    public int EnsureCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        if (_array.Length < capacity)
        {
            Grow(capacity);
            _version++;
        }

        return _array.Length;
    }

    void Grow(int capacity)
    {
        var newCapacity = _array.Length == 0 ? DefaultCapacity : 2 * _array.Length;

        if ((uint)newCapacity > Array.MaxLength) newCapacity = Array.MaxLength;

        if (newCapacity < capacity) newCapacity = capacity;

        var newArray = _pool.Rent(newCapacity);
        Array.Copy(_array, newArray, _size);
        _pool.Return(_array);
        _array = newArray;
    }

    public T[] ToArray()
    {
        if (_size == 0) return s_emptyArray;

        var objArray = new T[_size];
        var i = 0;
        while (i < _size)
        {
            objArray[i] = _array[_size - i - 1];
            i++;
        }

        return objArray;
    }

    void ReturnArray(T[] replaceWith = null)
    {
        if (_array is not null) _pool.Return(_array, s_clearArray);

        _array = replaceWith ?? s_emptyArray;
    }

    void ThrowForEmptyStack()
    {
        Debug.Assert(_size == 0);
        ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EmptyStack();
    }

    public void Dispose()
    {
        ReturnArray(s_emptyArray);
        _size = 0;
        _version++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped Span<T> dest) => CopyTo(dest, 0, _size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped Span<T> dest, int destIndex) => CopyTo(dest, destIndex, _size);

    public void CopyTo(scoped Span<T> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowArrayIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (dest.Length - destIndex < count || _size < count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        var src = _array.AsSpan(0, _size);

        if (src.Length == 0) return;

        var srcIndex = 0;
        var dstIndex = destIndex + count;
        while (srcIndex < count) dest[--dstIndex] = src[srcIndex++];
    }

    public struct Enumerator : IEnumerator<T>
    {
        readonly PooledStack<T> _stack;
        readonly int _version;
        int _index;
        T _currentElement;

        internal Enumerator(PooledStack<T> stack)
        {
            _stack = stack;
            _version = stack._version;
            _index = -2;
            _currentElement = default;
        }

        public void Dispose() => _index = -1;

        public bool MoveNext()
        {
            bool retval;
            if (_version != _stack._version)
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            switch (_index)
            {
                case -2:
                {
                    _index = _stack._size - 1;
                    retval = _index >= 0;
                    if (retval) _currentElement = _stack._array[_index];
                    return retval;
                }

                case -1: return false;
            }

            retval = --_index >= 0;
            _currentElement = retval ? _stack._array[_index] : default;

            return retval;
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
            if (_index == -2) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumNotStarted();
            else ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumEnded();
        }

        object IEnumerator.Current => Current;

        void IEnumerator.Reset()
        {
            if (_version != _stack._version)
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            _index = -2;
            _currentElement = default;
        }
    }
}