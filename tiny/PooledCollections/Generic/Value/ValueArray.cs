namespace Tiny.PooledCollections.Generic.Value;

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tiny.PooledCollections.Generic.Value.Internals;

public struct ValueArray<T> : IReadOnlyList<T>, IDisposable
{
    internal static readonly bool s_clearArray = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    static readonly T[] s_emptyArray = [];

    internal T[] _array;
    internal int _length;

    internal ArrayPool<T> Pool
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        init;
    }

    internal ValueArray(int length, ArrayPool<T> pool)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        _length = length;
        Pool = pool ?? ArrayPool<T>.Shared;
        _array = _length == 0 ? s_emptyArray : Pool.Rent(length);
    }

    internal ValueArray(scoped ReadOnlySpan<T> array, int length, ArrayPool<T> pool)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        Pool = pool ?? ArrayPool<T>.Shared;
        _length = length;
        _array = Pool.Rent(length);

        if (array.IsEmpty) return;

        array[..int.Min(array.Length, length)].CopyTo(_array);
    }

    public readonly int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _length;
    }

    public readonly int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.Length;
    }

    public readonly bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array is not null;
    }

    public readonly ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_length) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessException();
            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_array), index);
        }
    }

    int IReadOnlyCollection<T>.Count => _length;

    T IReadOnlyList<T>.this[int index] => this[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(T[] dest) => CopyTo(0, dest, 0, _length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(T[] dest, int destIndex) => CopyTo(0, dest, destIndex, _length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(T[] dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public readonly void CopyTo(int index, T[] dest, int destIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(dest);

        CopyTo(index, dest.AsSpan(), destIndex, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(ValueArray<T> dest) => CopyTo(0, dest, 0, _length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(ValueArray<T> dest, int destIndex) => CopyTo(0, dest, destIndex, _length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(ValueArray<T> dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(int index, ValueArray<T> dest, int destIndex, int count)
        => CopyTo(index, dest._array.AsSpan(), destIndex, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(scoped Span<T> dest) => CopyTo(0, dest, 0, _length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(scoped Span<T> dest, int destIndex) => CopyTo(0, dest, destIndex, _length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(scoped Span<T> dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public readonly void CopyTo(int index, scoped Span<T> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (dest.Length - destIndex < count || _length - index < count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        if (_length == 0) return;

        this.AsSpan(index, count).CopyTo(dest[destIndex..]);
    }

    public void Dispose()
    {
        if (_array is not null) Pool.Return(_array, s_clearArray);

        _array = s_emptyArray;
        _length = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Enumerator GetEnumerator() => new(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);
    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

    public struct Enumerator : IEnumerator<T>
    {
        readonly ValueArray<T> _array;
        int _index;

        internal Enumerator(ValueArray<T> array)
        {
            _array = array;
            _index = -1;
        }

        public bool MoveNext()
        {
            var index = _index + 1;
            if (index >= _array.Length) return false;

            _index = index;
            return true;
        }

        public readonly ref T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _array._array[_index];
        }

        public void Reset() => _index = -1;

        T IEnumerator<T>.Current => Current;
        object IEnumerator.Current => Current;

        void IDisposable.Dispose() { }
    }
}

public static class ValueArray
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(T[] array) => new(array, array.Length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(T[] array, ArrayPool<T> pool) => new(array, array.Length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(T[] array, int length) => new(array, length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(T[] array, int length, ArrayPool<T> pool)
        => new(new ReadOnlySpan<T>(array, 0, length), length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(int length) => new(length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(int length, ArrayPool<T> pool) => new(length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Empty<T>() => Create<T>(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining), OverloadResolutionPriority(1)]
    public static ValueArray<T> Create<T>(scoped ReadOnlySpan<T> array) => new(array, array.Length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining), OverloadResolutionPriority(1)]
    public static ValueArray<T> Create<T>(scoped ReadOnlySpan<T> array, ArrayPool<T> pool) => new(array, array.Length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining), OverloadResolutionPriority(1)]
    public static ValueArray<T> Create<T>(scoped ReadOnlySpan<T> array, int length)
        => new(array, length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining), OverloadResolutionPriority(1)]
    public static ValueArray<T> Create<T>(scoped ReadOnlySpan<T> array, int length, ArrayPool<T> pool)
        => new(array, length, pool);
}