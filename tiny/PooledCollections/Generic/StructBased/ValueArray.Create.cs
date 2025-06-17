namespace Tiny.PooledCollections.Generic.StructBased;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public static class ValueArray
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(T[] array) => Create(array, array.Length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(T[] array, ArrayPool<T> pool) => Create(array, array.Length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(T[] array, int length) => Create(array, length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(T[] array, int length, ArrayPool<T> pool)
        => Create(new ReadOnlySpan<T>(array, 0, length), pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(int length) => new(length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(int length, ArrayPool<T> pool) => new(length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Empty<T>() => Create<T>(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Empty<T>(ArrayPool<T> pool) => Create(0, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(scoped ReadOnlySpan<T> array) => new(in array, array.Length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(scoped ReadOnlySpan<T> array, ArrayPool<T> pool)
        => new(in array, array.Length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(scoped ReadOnlySpan<T> array, int length)
        => new(in array, length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(scoped ReadOnlySpan<T> array, int length, ArrayPool<T> pool)
        => new(in array, length, pool);
}