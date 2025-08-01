namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly ref struct QueueInternalsRef<T>
{
    public readonly int Head, Tail, Size, Version;
    public readonly bool ClearArray;
    public readonly ReadOnlySpan<T> Array;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal QueueInternalsRef(PooledQueue<T> source)
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
    public static QueueInternalsRef<T> GetRef<T>(this PooledQueue<T> source) => new(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this PooledQueue<T> source, out int head, out int tail)
    {
        head = source._head;
        tail = source._tail;
        return MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(source._array), source._size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this PooledQueue<T> source, out int head, out int tail)
    {
        head = source._head;
        tail = source._tail;
        return new(source._array, 0, source._size);
    }
}