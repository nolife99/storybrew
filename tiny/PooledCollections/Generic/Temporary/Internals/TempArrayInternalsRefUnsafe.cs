namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct TempArrayInternalsRefUnsafe<T>
{
    public int Length { get; }
    public bool ClearArray { get; }
    public T[] Array { get; }

    internal TempArrayInternalsRefUnsafe(scoped ref readonly TempArray<T> source)
    {
        Length = source._length;
        ClearArray = TempArray<T>.s_clearArray;
        Array = source._array;
    }
}

partial class TempCollectionInternalsUnsafe
{
    /// <summary> Returns a structure that holds references to internal fields of <paramref name="source"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArrayInternalsRefUnsafe<T> GetRef<T>(scoped ref readonly TempArray<T> source) => new(in source);

    /// <summary> Returns the internal array as a <see cref="Span{T}"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly TempArray<T> source)
        => MemoryMarshal.CreateSpan(ref source._ref, source._length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly TempArray<T> source, int start) => AsSpan(in source)[start..];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly TempArray<T> source, int start, int length)
        => AsSpan(in source)[start..length];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly TempArray<T> source, Index startIndex)
        => AsSpan(in source)[startIndex..];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly TempArray<T> source, Range range) => AsSpan(in source)[range];

    /// <summary> Returns the internal array as a <see cref="Memory{T}"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly TempArray<T> source)
        => source._array.AsMemory(0, source._length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly TempArray<T> source, int start)
        => source._array.AsMemory(start);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly TempArray<T> source, int start, int length)
        => source._array.AsMemory(start, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly TempArray<T> source, Index startIndex)
        => source._array.AsMemory(startIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly TempArray<T> source, Range range)
        => source._array.AsMemory(range);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this scoped ref readonly TempArray<T> source, out T[] array, out int length)
    {
        array = source._array;
        length = source._length;
    }
}