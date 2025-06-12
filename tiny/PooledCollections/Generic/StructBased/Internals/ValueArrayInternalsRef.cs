namespace Tiny.PooledCollections.Generic.StructBased.Internals;

using System;
using System.Runtime.CompilerServices;

public readonly ref struct ValueArrayInternalsRef<T>
{
    [NonSerialized] public readonly int Length;
    [NonSerialized] public readonly bool ClearArray;
    [NonSerialized] public readonly ReadOnlySpan<T> Array;

    internal ValueArrayInternalsRef(scoped ref readonly ValueArray<T> source)
    {
        Length = source._length;
        ClearArray = ValueArray<T>.s_clearArray;
        Array = source._array;
    }
}

partial class ValueCollectionInternals
{
    /// <summary>Returns a structure that holds references to internal fields of <paramref name="source"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArrayInternalsRef<T> GetRef<T>(this scoped ref readonly ValueArray<T> source) => new(in source);

    /// <summary>Returns the internal array as a <see cref="ReadOnlySpan{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ValueArray<T> source)
        => source._array.AsSpan(0, source._length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ValueArray<T> source, int start)
        => source._array.AsSpan(start);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ValueArray<T> source, int start, int length)
        => source._array.AsSpan(start, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ValueArray<T> source, Index startIndex)
        => source._array.AsSpan(startIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ValueArray<T> source, Range range)
        => source._array.AsSpan(range);

    /// <summary>Returns the internal array as a <see cref="ReadOnlyMemory{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueArray<T> source)
        => source._array.AsMemory(0, source._length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueArray<T> source, int start)
        => source._array.AsMemory(start);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueArray<T> source, int start, int length)
        => source._array.AsMemory(start, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueArray<T> source, Index startIndex)
        => source._array.AsMemory(startIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueArray<T> source, Range range)
        => source._array.AsMemory(range);
}