namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct ValueStackInternalsRefUnsafe<T>
{
    public readonly int Size, Version;
    public readonly bool ClearArray;
    public readonly T[] Array;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueStackInternalsRefUnsafe(scoped ref readonly ValueStack<T> source)
    {
        Size = source._size;
        Version = source._version;
        ClearArray = ValueStack<T>.s_clearArray;
        Array = source._array;
    }
}

partial class CollectionInternals
{
    public static ValueStackInternalsRefUnsafe<T> GetUnsafeRef<T>(this scoped ref readonly ValueStack<T> source)
        => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly ValueStack<T> source)
        => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(source._array), source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly ValueStack<T> source)
        => new(source._array, 0, source._size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this scoped ref readonly ValueStack<T> source, out T[] array, out int count)
    {
        array = source._array;
        count = source._size;
    }
}