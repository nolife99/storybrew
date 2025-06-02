namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

partial struct TempArray<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create(int length) => new(length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create(int length, ArrayPool<T> pool) => new(length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Empty() => Create(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Empty(ArrayPool<T> pool) => Create(0, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create(scoped ReadOnlySpan<T> array) => new(array, array.Length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create(scoped ReadOnlySpan<T> array, ArrayPool<T> pool) => new(array, array.Length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create(scoped ReadOnlySpan<T> array, int length) => new(array, length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create(scoped ReadOnlySpan<T> array, int length, ArrayPool<T> pool)
        => new(array, length, pool);
}

public static class TempArray
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array) => TempArray<T>.Create(array, array.Length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array, ArrayPool<T> pool) => TempArray<T>.Create(array, array.Length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array, int length) => TempArray<T>.Create(array, length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array, int length, ArrayPool<T> pool)
        => TempArray<T>.Create(array, length, pool);
}