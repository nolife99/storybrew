namespace Tiny.PooledCollections.Generic.StructBased.Internals;

using System;
using System.Runtime.CompilerServices;

partial class ValueCollectionInternals
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ReadOnlyValueArray<T> source)
        => source._array.AsReadOnlySpan();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ReadOnlyValueArray<T> source, int start)
        => source._array.AsReadOnlySpan(start);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ReadOnlyValueArray<T> source,
        int start,
        int length) => source._array.AsReadOnlySpan(start, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ReadOnlyValueArray<T> source, Index index)
        => source._array.AsReadOnlySpan(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ReadOnlyValueArray<T> source, Range range)
        => source._array.AsReadOnlySpan(range);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ReadOnlyValueArray<T> source)
        => source._array.AsReadOnlyMemory();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ReadOnlyValueArray<T> source, int start)
        => source._array.AsReadOnlyMemory(start);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ReadOnlyValueArray<T> source,
        int start,
        int length) => source._array.AsReadOnlyMemory(start, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ReadOnlyValueArray<T> source, Index index)
        => source._array.AsReadOnlyMemory(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ReadOnlyValueArray<T> source, Range range)
        => source._array.AsReadOnlyMemory(range);
}