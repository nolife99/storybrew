#pragma warning disable CS8632

namespace Tiny.PooledCollections.Generic.StructBased;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public partial struct ValueStack<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueStack<T> Create() => new(0, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueStack<T> Create(int capacity) => new(capacity, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueStack<T> Create(IEnumerable<T> collection) => new(collection, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueStack<T> Create(ArrayPool<T> pool) => new(0, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueStack<T> Create(int capacity, ArrayPool<T> pool) => new(capacity, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueStack<T> Create(IEnumerable<T> collection, ArrayPool<T> pool) => new(collection, pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueStack<T> Create(T[] items) => new(items.AsSpan(), ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueStack<T> Create(T[] items, ArrayPool<T> pool) => new(items.AsSpan(), pool);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueStack<T> Create(ReadOnlySpan<T> span) => new(span, ArrayPool<T>.Shared);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueStack<T> Create(ReadOnlySpan<T> span, ArrayPool<T> pool) => new(span, pool);
}