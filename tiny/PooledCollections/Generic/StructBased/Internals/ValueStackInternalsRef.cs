namespace Tiny.PooledCollections.Generic.StructBased.Internals;

using System;
using System.Runtime.CompilerServices;

public readonly ref struct ValueStackInternalsRef<T>
{
    [NonSerialized] public readonly int Size;
    [NonSerialized] public readonly int Version;
    [NonSerialized] public readonly bool ClearArray;
    [NonSerialized] public readonly ReadOnlySpan<T> Array;

    internal ValueStackInternalsRef(scoped ref readonly ValueStack<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearArray = ValueStack<T>.s_clearArray;
        Array = source._array;
    }
}

partial class ValueCollectionInternals
{
    /// <summary>Returns a structure that holds references to internal fields of <paramref name="source"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueStackInternalsRef<T> GetRef<T>(this scoped ref readonly ValueStack<T> source) => new(in source);

    /// <summary>Returns the internal array as a <see cref="ReadOnlySpan{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ValueStack<T> source)
        => source._array.AsSpan(0, source._size);

    /// <summary>Returns the internal array as a <see cref="ReadOnlyMemory{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueStack<T> source)
        => source._array.AsMemory(0, source._size);
}