namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Runtime.CompilerServices;

public readonly ref struct TempStackInternalsRef<T>
{
    [NonSerialized] public readonly int Size;
    [NonSerialized] public readonly int Version;
    [NonSerialized] public readonly bool ClearArray;
    [NonSerialized] public readonly ReadOnlySpan<T> Array;

    internal TempStackInternalsRef(scoped ref readonly TempStack<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearArray = TempStack<T>.s_clearArray;
        Array = source._array;
    }
}

partial class TempCollectionInternals
{
    /// <summary> Returns a structure that holds references to internal fields of <paramref name="source"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempStackInternalsRef<T> GetRef<T>(this scoped ref readonly TempStack<T> source) => new(in source);

    /// <summary> Returns the internal array as a <see cref="ReadOnlySpan{T}"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly TempStack<T> source)
        => source._array.AsSpan(0, source._size);

    /// <summary> Returns the internal array as a <see cref="ReadOnlyMemory{T}"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly TempStack<T> source)
        => source._array.AsMemory(0, source._size);
}