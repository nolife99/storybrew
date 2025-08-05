namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct ValueQueueInternalsRefUnsafe<T>
{
    public readonly int Head, Tail, Size, Version;
    public readonly bool ClearArray;
    public readonly T[] Array;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueQueueInternalsRefUnsafe(scoped ref readonly ValueQueue<T> source)
    {
        Head = source._head;
        Tail = source._tail;
        Size = source._size;
        Version = source._version;
        ClearArray = ValueQueue<T>.s_clearArray;
        Array = source._array;
    }
}

partial class CollectionInternals
{
    public static ValueQueueInternalsRefUnsafe<T> GetUnsafeRef<T>(this scoped ref readonly ValueQueue<T> source)
        => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this scoped ref readonly ValueQueue<T> source, out int head, out int tail)
    {
        head = source._head;
        tail = source._tail;
        return MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(source._array), source._size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this scoped ref readonly ValueQueue<T> source, out int head, out int tail)
    {
        head = source._head;
        tail = source._tail;
        return new(source._array, 0, source._size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this scoped ref readonly ValueQueue<T> source,
        out T[] array,
        out int count,
        out int head,
        out int tail)
    {
        array = source._array;
        count = source._size;
        head = source._head;
        tail = source._tail;
    }
}