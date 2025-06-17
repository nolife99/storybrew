namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public ref struct TempArray<T>
{
    internal static readonly bool s_clearArray = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    static readonly T[] s_emptyArray = [];

    internal T[] _array; // Do not rename (binary serialization)
    internal int _length; // Do not rename (binary serialization)

    [NonSerialized] internal ArrayPool<T> _pool;

    internal TempArray(int length, ArrayPool<T> pool)
    {
        if (length < 0) ThrowHelper.ThrowLengthArgumentOutOfRange_ArgumentOutOfRange_NeedNonNegNum();

        _length = length;
        _pool = pool ?? ArrayPool<T>.Shared;
        _array = _length == 0 ? s_emptyArray : _pool.Rent(length);
        _ref = ref MemoryMarshal.GetArrayDataReference(_array);
    }

    internal TempArray(scoped ReadOnlySpan<T> array, int length, ArrayPool<T> pool)
    {
        if (length < 0) ThrowHelper.ThrowLengthArgumentOutOfRange_ArgumentOutOfRange_NeedNonNegNum();

        _pool = pool ?? ArrayPool<T>.Shared;
        _length = length;
        _array = _pool.Rent(length);
        _ref = ref MemoryMarshal.GetArrayDataReference(_array);

        if (array.IsEmpty) return;

        var minLength = Math.Min(array.Length, length);

        Unsafe.CopyBlockUnaligned(ref Unsafe.As<T, byte>(ref _ref),
            ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(array)),
            (uint)(minLength * Unsafe.SizeOf<T>()));
    }

    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _length;
    }

    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.Length;
    }

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array is not null;
    }

    internal ref T _ref;

    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_length) ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLessException();
            return ref Unsafe.Add(ref _ref, index);
        }
    }

    /// <summary>Copies this List into array, which must be of a compatible array type.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest) => CopyTo(0, dest, 0, _length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest, int destIndex) => CopyTo(0, dest, destIndex, _length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public void CopyTo(int index, T[] dest, int destIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(dest);

        CopyTo(index, dest.AsSpan(), destIndex, count);
    }

    /// <summary>Copies this List into array, which must be of a compatible array type.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(TempArray<T> dest) => CopyTo(0, dest, 0, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(TempArray<T> dest, int destIndex) => CopyTo(0, dest, destIndex, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(TempArray<T> dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(int index, TempArray<T> dest, int destIndex, int count)
        => CopyTo(index, dest._array.AsSpan(), destIndex, count);

    /// <summary>Copies this List into the given span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<T> dest) => CopyTo(0, dest, 0, _length);

    /// <summary>Copies this List into the given span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<T> dest, int destIndex) => CopyTo(0, dest, destIndex, _length);

    /// <summary>Copies this List into the given span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<T> dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public void CopyTo(int index, Span<T> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        if (count < 0) ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_NeedNonNegNum();

        if (dest.Length - destIndex < count || _length - index < count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);

        var src = _array.AsSpan(0, _length);

        if (src.Length == 0) return;

        src.Slice(index, count).CopyTo(dest.Slice(destIndex, count));
    }

    void ReturnArray(T[] replaceWith)
    {
        if (IsValid) _pool?.Return(_array, s_clearArray);

        _array = replaceWith ?? s_emptyArray;
        _ref = ref MemoryMarshal.GetArrayDataReference(_array);
    }

    public void Dispose()
    {
        ReturnArray(s_emptyArray);
        _length = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(ref this);

    public ref struct Enumerator
    {
        readonly TempArray<T> _array;
        int _index;

        internal Enumerator(scoped ref TempArray<T> array)
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

        public ref T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.Add(ref _array._ref, _index);
        }

        public void Reset()
        {
            _index = 0;
            Current = default;
        }

        public void Dispose() { }
    }
}