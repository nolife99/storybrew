namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public static class TempArray
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(int length) => new(int.Max(length, 0), ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(int length, ArrayPool<T> pool) => new(int.Max(length, 0), pool);

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
        => new(array, int.Clamp(length, 0, array.Length), ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(scoped ReadOnlySpan<T> array, int length, ArrayPool<T> pool)
        => new(array, int.Clamp(length, 0, array.Length), pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array) => Create(array, array.Length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array, ArrayPool<T> pool) => Create(array, array.Length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array, int length)
        => Create(array, int.Clamp(length, 0, array.Length), ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArray<T> Create<T>(T[] array, int length, ArrayPool<T> pool)
        => Create(array, int.Clamp(length, 0, array.Length), pool);
}