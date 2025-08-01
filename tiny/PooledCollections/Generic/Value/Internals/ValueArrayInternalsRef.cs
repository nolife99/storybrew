namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly ref struct ValueArrayInternalsRef<T>
{
    public readonly int Length;
    public readonly bool ClearArray;
    public readonly ReadOnlySpan<T> Array;

    internal ValueArrayInternalsRef(scoped ref readonly ValueArray<T> source)
    {
        Length = source._length;
        ClearArray = ValueArray<T>.s_clearArray;
        Array = source._array;
    }
}

partial class CollectionInternals
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueArrayInternalsRef<T> GetRef<T>(this scoped ref readonly ValueArray<T> source) => new(in source);

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
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ValueArray<T> source, Index index)
        => AsReadOnlySpan(in source)[index..];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ValueArray<T> source, Range range)
        => AsReadOnlySpan(in source)[range];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueArray<T> source)
        => new(source._array, 0, source._length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueArray<T> source, int start)
        => new(source._array, start, source._length - start);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueArray<T> source, int start, int length)
        => AsReadOnlyMemory(in source)[start..length];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueArray<T> source, Index index)
        => AsReadOnlyMemory(in source)[index..];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueArray<T> source, Range range)
        => AsReadOnlyMemory(in source)[range];
}