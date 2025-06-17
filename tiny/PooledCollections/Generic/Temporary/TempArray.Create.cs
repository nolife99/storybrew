namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public static class TempArray
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(int length) => new(length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(int length, ArrayPool<T> pool) => new(length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Empty<T>() => Create<T>(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Empty<T>(ArrayPool<T> pool) => Create(0, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(scoped ReadOnlySpan<T> array) => new(array, array.Length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(scoped ReadOnlySpan<T> array, ArrayPool<T> pool) => new(array, array.Length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(scoped ReadOnlySpan<T> array, int length)
        => new(array, length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(scoped ReadOnlySpan<T> array, int length, ArrayPool<T> pool)
        => new(array, length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array) => Create(array, array.Length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array, ArrayPool<T> pool) => Create(array, array.Length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array, int length) => Create(array, length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array, int length, ArrayPool<T> pool) => Create(array, length, pool);
}