namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public ref struct ReadOnlyTempArray<T>
{
    internal TempArray<T> _array;

    ReadOnlyTempArray(TempArray<T> array) => _array = array;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyTempArray<T> Empty() => new(TempArray.Empty<T>());

    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array[index];
    }

    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.Length;
    }

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.IsValid;
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

        CopyTo(index, new Span<T>(dest), destIndex, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped TempArray<T> dest) => CopyTo(0, dest, 0, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped TempArray<T> dest, int destIndex) => CopyTo(0, dest, destIndex, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped TempArray<T> dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(int index, scoped TempArray<T> dest, int destIndex, int count)
        => CopyTo(index, MemoryMarshal.CreateSpan(ref dest._ref, count), destIndex, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped Span<T> dest) => CopyTo(0, dest, 0, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped Span<T> dest, int destIndex) => CopyTo(0, dest, destIndex, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(scoped Span<T> dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public void CopyTo(int index, scoped Span<T> dest, int destIndex, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(destIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(destIndex, dest.Length);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (dest.Length - destIndex < count || _array.Length - index < count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        var src = _array._array.AsSpan(0, _array._length);

        if (src.Length == 0) return;

        src.Slice(index, count).CopyTo(dest.Slice(destIndex, count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TempArray<T>.Enumerator GetEnumerator() => _array.GetEnumerator();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _array.Dispose();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlyTempArray<T>(TempArray<T> array) => new(array);
}