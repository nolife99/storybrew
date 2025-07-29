namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static class TempList
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempList<T> Create<T>() => new(0, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempList<T> Create<T>(int capacity) => new(capacity, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempList<T> Create<T>(ArrayPool<T> pool) => new(0, pool ?? ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempList<T> Create<T>(int capacity, ArrayPool<T> pool) => new(capacity, pool ?? ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempList<T> Create<T>(IEnumerable<T> collection) => new(collection, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempList<T> Create<T>(IEnumerable<T> collection, ArrayPool<T> pool)
        => new(collection, pool ?? ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempList<T> Create<T>(T[] items) => new(new ReadOnlySpan<T>(items), ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempList<T> Create<T>(T[] items, ArrayPool<T> pool)
        => new(new ReadOnlySpan<T>(items), pool ?? ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining), OverloadResolutionPriority(1)]
    public static TempList<T> Create<T>(scoped ReadOnlySpan<T> span) => new(span, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining), OverloadResolutionPriority(1)]
    public static TempList<T> Create<T>(scoped ReadOnlySpan<T> span, ArrayPool<T> pool)
        => new(span, pool ?? ArrayPool<T>.Shared);
}