namespace Tiny.PooledCollections.Generic.StructBased.Internals;

using System;
using System.Runtime.CompilerServices;

public readonly struct ValueStackInternalsRefUnsafe<T>
{
    [NonSerialized] public readonly int Size;
    [NonSerialized] public readonly int Version;
    [NonSerialized] public readonly bool ClearArray;
    [NonSerialized] public readonly T[] Array;

    internal ValueStackInternalsRefUnsafe(scoped ref readonly ValueStack<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearArray = ValueStack<T>.s_clearArray;
        Array = source._array;
    }
}

partial class ValueCollectionInternalsUnsafe
{
    /// <summary>Returns a structure that holds references to internal fields of <paramref name="source"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueStackInternalsRefUnsafe<T> GetRef<T>(this scoped ref readonly ValueStack<T> source) => new(in source);

    /// <summary>Returns the internal array as a <see cref="Span{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly ValueStack<T> source) => source._array.AsSpan(0, source._size);

    /// <summary>Returns the internal array as a <see cref="Memory{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly ValueStack<T> source)
        => source._array.AsMemory(0, source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this scoped ref readonly ValueStack<T> source, out T[] array, out int count)
    {
        array = source._array;
        count = source._size;
    }
}