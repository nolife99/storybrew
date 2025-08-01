namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct QueueInternalsRefUnsafe<T>
{
    public readonly int Head, Tail, Size, Version;
    public readonly bool ClearArray;
    public readonly T[] Array;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal QueueInternalsRefUnsafe(PooledQueue<T> source)
    {
        Head = source._head;
        Tail = source._tail;
        Size = source._size;
        Version = source._version;
        ClearArray = PooledQueue<T>.s_clearArray;
        Array = source._array;
    }
}

partial class CollectionInternals
{
    public static QueueInternalsRefUnsafe<T> GetUnsafeRef<T>(this PooledQueue<T> source) => new(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this PooledQueue<T> source, out int head, out int tail)
    {
        head = source._head;
        tail = source._tail;
        return MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(source._array), source._size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this PooledQueue<T> source, out int head, out int tail)
    {
        head = source._head;
        tail = source._tail;
        return new(source._array, 0, source._size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this PooledQueue<T> source, out T[] array, out int count, out int head, out int tail)
    {
        array = source._array;
        count = source._size;
        head = source._head;
        tail = source._tail;
    }
}