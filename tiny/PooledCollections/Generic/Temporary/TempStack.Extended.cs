namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

partial struct TempStack<T>
{
    internal TempStack(ReadOnlySpan<T> span, ArrayPool<T> pool)
    {
        _size = 0;
        _version = 0;
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
    public void CopyTo(in Span<T> dest) => CopyTo(dest, 0, _size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in Span<T> dest, int destIndex) => CopyTo(dest, destIndex, _size);

    public void CopyTo(in Span<T> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowArrayIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        if (count < 0) ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_NeedNonNegNum();

        if (dest.Length - destIndex < count || _size < count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        var src = _array.AsSpan(0, _size);

        if (src.Length == 0) return;

        var srcIndex = 0;
        var dstIndex = destIndex + count;
        while (srcIndex < count) dest[--dstIndex] = src[srcIndex++];
    }

    public void Dispose()
    {
        ReturnArray(s_emptyArray);
        _size = 0;
        _version++;
    }
}