namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static class TempQueue
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempQueue<T> Create<T>() => new(0, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempQueue<T> Create<T>(int capacity) => new(capacity, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempQueue<T> Create<T>(IEnumerable<T> collection) => new(collection, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempQueue<T> Create<T>(ArrayPool<T> pool) => new(0, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempQueue<T> Create<T>(int capacity, ArrayPool<T> pool) => new(capacity, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempQueue<T> Create<T>(IEnumerable<T> collection, ArrayPool<T> pool) => new(collection, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempQueue<T> Create<T>(T[] items) => new(items.AsSpan(), ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempQueue<T> Create<T>(T[] items, ArrayPool<T> pool) => new(items.AsSpan(), pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempQueue<T> Create<T>(ReadOnlySpan<T> span) => new(span, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempQueue<T> Create<T>(ReadOnlySpan<T> span, ArrayPool<T> pool) => new(span, pool);
}