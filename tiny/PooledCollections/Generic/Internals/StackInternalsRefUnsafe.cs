namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Runtime.CompilerServices;

public readonly struct StackInternalsRefUnsafe<T>
{
    public readonly int Size, Version;
    public readonly bool ClearArray;
    public readonly T[] Array;

    internal StackInternalsRefUnsafe(PooledStack<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearArray = PooledStack<T>.s_clearArray;
        Array = source._array;
    }
}

partial class CollectionInternalsUnsafe
{
    /// <summary> Returns a structure that holds references to internal fields of <paramref name="source"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StackInternalsRefUnsafe<T> GetRef<T>(PooledStack<T> source) => new(source);

    /// <summary> Returns the internal array as a <see cref="Span{T}"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this PooledStack<T> source) => source._array.AsSpan(0, source._size);

    /// <summary> Returns the internal array as a <see cref="Memory{T}"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this PooledStack<T> source) => source._array.AsMemory(0, source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this PooledStack<T> source, out T[] array, out int count)
    {
        array = source._array;
        count = source._size;
    }
}