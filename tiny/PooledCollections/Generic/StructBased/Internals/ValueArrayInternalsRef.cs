namespace Tiny.PooledCollections.Generic.StructBased.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
        => MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(source._array), source._length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ValueArray<T> source, int start)
        => AsReadOnlySpan(in source)[start..];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ValueArray<T> source, int start, int length)
        => AsReadOnlySpan(in source)[start..length];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ValueArray<T> source, Index startIndex)
        => AsReadOnlySpan(in source)[startIndex..];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ValueArray<T> source, Range range)
        => AsReadOnlySpan(in source)[range];

    /// <summary>Returns the internal array as a <see cref="ReadOnlyMemory{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueArray<T> source)
        => new(source._array, 0, source._length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueArray<T> source, int start)
        => new(source._array, start, source._length - start);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueArray<T> source, int start, int length)
        => new(source._array, start, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueArray<T> source, Index startIndex)
        => source._array.AsMemory(startIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueArray<T> source, Range range)
        => source._array.AsMemory(range);
}