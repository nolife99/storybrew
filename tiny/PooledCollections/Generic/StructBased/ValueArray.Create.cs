namespace Tiny.PooledCollections.Generic.StructBased;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

partial struct ValueArray<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create(int length) => new(length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create(int length, ArrayPool<T> pool) => new(length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Empty() => Create(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Empty(ArrayPool<T> pool) => Create(0, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create(ReadOnlySpan<T> array) => new(array, array.Length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create(ReadOnlySpan<T> array, ArrayPool<T> pool) => new(array, array.Length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create(ReadOnlySpan<T> array, int length) => new(array, length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create(ReadOnlySpan<T> array, int length, ArrayPool<T> pool) => new(array, length, pool);
}

public static class ValueArray
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(T[] array) => ValueArray<T>.Create(array, array.Length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(T[] array, ArrayPool<T> pool) => ValueArray<T>.Create(array, array.Length, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(T[] array, int length) => ValueArray<T>.Create(array, length, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArray<T> Create<T>(T[] array, int length, ArrayPool<T> pool)
        => ValueArray<T>.Create(array, length, pool);
}