namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly ref struct TempArrayInternalsRef<T>
{
    public int Length { get; }
    public bool ClearArray { get; }
    public ReadOnlySpan<T> Array { get; }

    internal TempArrayInternalsRef(scoped ref readonly TempArray<T> source)
    {
        Length = source._length;
        ClearArray = TempArray<T>.s_clearArray;
        Array = source._array;
    }
}

partial class TempCollectionInternals
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempArrayInternalsRef<T> GetRef<T>(this scoped ref readonly TempArray<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly TempArray<T> source)
        => MemoryMarshal.CreateReadOnlySpan(ref source._ref, source._length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly TempArray<T> source, int start)
        => AsReadOnlySpan(in source)[start..];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly TempArray<T> source, int start, int length)
        => AsReadOnlySpan(in source)[start..length];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly TempArray<T> source, Index startIndex)
        => AsReadOnlySpan(in source)[startIndex..];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly TempArray<T> source, Range range)
        => AsReadOnlySpan(in source)[range];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly TempArray<T> source)
        => new(source._array, 0, source._length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly TempArray<T> source, int start)
        => new(source._array, start, source._length - start);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly TempArray<T> source, int start, int length)
        => source._array.AsMemory(start, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly TempArray<T> source, Index startIndex)
        => source._array.AsMemory(startIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly TempArray<T> source, Range range)
        => source._array.AsMemory(range);
}