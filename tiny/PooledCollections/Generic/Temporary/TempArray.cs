namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public ref struct TempArray<T>
{
    internal static readonly bool s_clearArray = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    static readonly T[] s_emptyArray = [];

    internal T[] _array;
    internal int _length;

    internal ArrayPool<T> _pool;

    internal TempArray(int length, ArrayPool<T> pool)
    {
        _length = length;
        _pool = pool ?? ArrayPool<T>.Shared;
        _array = _length == 0 ? s_emptyArray : _pool.Rent(length);
        _ref = ref MemoryMarshal.GetArrayDataReference(_array);
    }

    internal TempArray(scoped ReadOnlySpan<T> array, int length, ArrayPool<T> pool)
    {
        _pool = pool ?? ArrayPool<T>.Shared;
        _length = length;
        _array = _pool.Rent(length);
        _ref = ref MemoryMarshal.GetArrayDataReference(_array);

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

    internal ref T _ref;

    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)_length);
            return ref Unsafe.Add(ref _ref, index);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(T[] dest) => CopyTo(0, dest, 0, _length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(T[] dest, int destIndex) => CopyTo(0, dest, destIndex, _length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(T[] dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public readonly void CopyTo(int index, T[] dest, int destIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(dest);

        CopyTo(index, new Span<T>(dest), destIndex, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(TempArray<T> dest) => CopyTo(0, dest, 0, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(TempArray<T> dest, int destIndex) => CopyTo(0, dest, destIndex, _array.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(TempArray<T> dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(int index, TempArray<T> dest, int destIndex, int count)
        => CopyTo(index, MemoryMarshal.CreateSpan(ref dest._ref, count), destIndex, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(Span<T> dest) => CopyTo(0, dest, 0, _length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(Span<T> dest, int destIndex) => CopyTo(0, dest, destIndex, _length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(Span<T> dest, int destIndex, int count) => CopyTo(0, dest, destIndex, count);

    public readonly void CopyTo(int index, Span<T> dest, int destIndex, int count)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        ArgumentOutOfRangeException.ThrowIfNegative(count);

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
    }
}

public static class TempArray
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(int length) => new(int.Max(length, 0), ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(int length, ArrayPool<T> pool) => new(int.Max(length, 0), pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Empty<T>() => Create<T>(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Empty<T>(ArrayPool<T> pool) => new(0, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(scoped ReadOnlySpan<T> array) => new(array, array.Length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(scoped ReadOnlySpan<T> array, ArrayPool<T> pool) => new(array, array.Length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(scoped ReadOnlySpan<T> array, int length)
        => new(array, int.Clamp(length, 0, array.Length), ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(scoped ReadOnlySpan<T> array, int length, ArrayPool<T> pool)
        => new(array, int.Clamp(length, 0, array.Length), pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array) => new(array, array.Length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array, ArrayPool<T> pool) => new(array, array.Length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array, int length)
        => new(array, int.Clamp(length, 0, array.Length), ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array, int length, ArrayPool<T> pool)
        => new(array, int.Clamp(length, 0, array.Length), pool);
}