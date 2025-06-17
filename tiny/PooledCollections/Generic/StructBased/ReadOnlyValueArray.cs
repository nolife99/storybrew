namespace Tiny.PooledCollections.Generic.StructBased;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public readonly struct ReadOnlyValueArray<T> : IReadOnlyList<T>, IDisposable
{
    internal readonly ValueArray<T> _array;

    ReadOnlyValueArray(in ValueArray<T> array) => _array = array;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyValueArray<T> Empty() => new(ValueArray.Empty<T>());

    T IReadOnlyList<T>.this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array[index];
    }

    public ref readonly T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _array[index];
    }

    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest) => CopyTo(0, dest, 0, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest, int destIndex) => CopyTo(0, dest, destIndex, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public void CopyTo(int index, T[] dest, int destIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(dest);

        CopyTo(index, dest.AsSpan(), destIndex, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in ValueArray<T> dest) => CopyTo(0, in dest, 0, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in ValueArray<T> dest, int destIndex) => CopyTo(0, in dest, destIndex, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in ValueArray<T> dest, int destIndex, int count) => CopyTo(0, in dest, destIndex, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(int index, in ValueArray<T> dest, int destIndex, int count)
        => CopyTo(index, dest._array.AsSpan(), destIndex, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped in Span<T> dest) => CopyTo(0, in dest, 0, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped in Span<T> dest, int destIndex) => CopyTo(0, in dest, destIndex, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped in Span<T> dest, int destIndex, int count) => CopyTo(0, in dest, destIndex, count);

    public void CopyTo(int index, scoped in Span<T> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        if (count < 0) ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_NeedNonNegNum();

        if (dest.Length - destIndex < count || _array.Length - index < count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        var src = _array._array.AsSpan(0, _array._length);

        if (src.Length == 0) return;

        src.Slice(index, count).CopyTo(dest.Slice(destIndex, count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueArray<T>.Enumerator GetEnumerator() => new(_array);

    int IReadOnlyCollection<T>.Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.Length;
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => new ValueArray<T>.Enumerator(_array);
    IEnumerator IEnumerable.GetEnumerator() => new ValueArray<T>.Enumerator(_array);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _array.Dispose();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlyValueArray<T>(in ValueArray<T> array) => new(array);
}