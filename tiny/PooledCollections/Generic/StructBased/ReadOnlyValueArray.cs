namespace Tiny.PooledCollections.Generic.StructBased;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Internals;

public struct ReadOnlyValueArray<T> : IReadOnlyList<T>, IDisposable
{
    internal ValueArray<T> _array;

    ReadOnlyValueArray(ValueArray<T> array) => _array = array;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyValueArray<T> Empty() => new(ValueArray.Empty<T>());

    T IReadOnlyList<T>.this[int index] => _array[index];

    public ref readonly T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _array[index];
    }

    public readonly int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array._length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(T[] dest) => CopyTo(0, dest, 0, _array._length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(T[] dest, int destIndex) => CopyTo(0, dest, destIndex, _array._length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(T[] dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public readonly void CopyTo(int index, T[] dest, int destIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(dest);

        CopyTo(index, dest.AsSpan(), destIndex, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(ValueArray<T> dest) => CopyTo(0, dest, 0, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(ValueArray<T> dest, int destIndex) => CopyTo(0, dest, destIndex, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(ValueArray<T> dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(int index, ValueArray<T> dest, int destIndex, int count)
        => CopyTo(index, dest._array.AsSpan(), destIndex, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(scoped Span<T> dest) => CopyTo(0, dest, 0, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(scoped Span<T> dest, int destIndex) => CopyTo(0, dest, destIndex, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(scoped Span<T> dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public readonly void CopyTo(int index, scoped Span<T> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (dest.Length - destIndex < count || _array.Length - index < count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        if (_array._length == 0) return;

        _array.AsSpan(index, count).CopyTo(dest.Slice(destIndex));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ValueArray<T>.Enumerator GetEnumerator() => new(_array);

    int IReadOnlyCollection<T>.Count => _array._length;

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => new ValueArray<T>.Enumerator(_array);
    IEnumerator IEnumerable.GetEnumerator() => new ValueArray<T>.Enumerator(_array);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _array.Dispose();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlyValueArray<T>(ValueArray<T> array) => new(array);
}