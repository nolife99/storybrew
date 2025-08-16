namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct ValueArrayInternalsRefUnsafe<T>
{
    public readonly int Length;
    public readonly bool ClearArray;
    public readonly T[] Array;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueArrayInternalsRefUnsafe(scoped ref readonly ValueArray<T> source)
    {
        Length = source._length;
        ClearArray = ValueArray<T>.s_clearArray;
        Array = source._array;
    }
}

partial class CollectionInternals
{
    public static ValueArrayInternalsRefUnsafe<T> GetUnsafeRef<T>(this scoped ref readonly ValueArray<T> source)
        => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly ValueArray<T> source)
        => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(source._array), source._length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly ValueArray<T> source, int start)
        => AsSpan(in source)[start..];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly ValueArray<T> source, int start, int length)
        => AsSpan(in source)[start..length];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly ValueArray<T> source, Index index)
        => AsSpan(in source)[index..];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly ValueArray<T> source, Range range)
        => AsSpan(in source)[range];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly ValueArray<T> source)
        => new(source._array, 0, source._length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly ValueArray<T> source, int start)
        => new(source._array, start, source._length - start);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly ValueArray<T> source, int start, int length)
        => AsMemory(in source)[start..length];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly ValueArray<T> source, Index index)
        => AsMemory(in source)[index..];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly ValueArray<T> source, Range range)
        => AsMemory(in source)[range];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this scoped ref readonly ValueArray<T> source, out T[] array, out int length)
    {
        array = source._array;
        length = source._length;
    }
}