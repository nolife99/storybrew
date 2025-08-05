namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly ref struct ValueStackInternalsRef<T>
{
    public readonly int Size, Version;
    public readonly bool ClearArray;
    public readonly ReadOnlySpan<T> Array;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueStackInternalsRef(scoped ref readonly ValueStack<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearArray = ValueStack<T>.s_clearArray;
        Array = source._array;
    }
}

partial class CollectionInternals
{
    public static ValueStackInternalsRef<T> GetRef<T>(this scoped ref readonly ValueStack<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ValueStack<T> source)
        => MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(source._array), source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueStack<T> source)
        => new(source._array, 0, source._size);
}