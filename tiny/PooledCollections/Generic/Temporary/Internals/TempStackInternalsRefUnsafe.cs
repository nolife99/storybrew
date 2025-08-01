namespace Tiny.PooledCollections.Generic.Temporary.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct TempStackInternalsRefUnsafe<T>
{
    public readonly int Size, Version;
    public readonly bool ClearArray;
    public readonly T[] Array;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TempStackInternalsRefUnsafe(scoped ref readonly TempStack<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearArray = TempStack<T>.s_clearArray;
        Array = source._array;
    }
}

partial class CollectionInternals
{
    public static TempStackInternalsRefUnsafe<T> GetUnsafeRef<T>(this scoped ref readonly TempStack<T> source)
        => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly TempStack<T> source)
        => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(source._array), source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly TempStack<T> source) => new(source._array, 0, source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this scoped ref readonly TempStack<T> source, out T[] array, out int count)
    {
        array = source._array;
        count = source._size;
    }
}