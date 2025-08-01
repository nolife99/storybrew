namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly ref struct StackInternalsRef<T>
{
    public readonly int Size, Version;
    public readonly bool ClearArray;
    public readonly ReadOnlySpan<T> Array;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal StackInternalsRef(PooledStack<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearArray = PooledStack<T>.s_clearArray;
        Array = source._array;
    }
}

partial class CollectionInternals
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StackInternalsRef<T> GetRef<T>(this PooledStack<T> source) => new(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this PooledStack<T> source)
        => MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(source._array), source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this PooledStack<T> source) => new(source._array, 0, source._size);
}