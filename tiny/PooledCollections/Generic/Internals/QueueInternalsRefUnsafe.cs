namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Runtime.CompilerServices;

public readonly struct QueueInternalsRefUnsafe<T>
{
    [NonSerialized] public readonly int Head;
    [NonSerialized] public readonly int Tail;
    [NonSerialized] public readonly int Size;
    [NonSerialized] public readonly int Version;
    [NonSerialized] public readonly bool ClearArray;
    [NonSerialized] public readonly T[] Array;

    public QueueInternalsRefUnsafe(PooledQueue<T> source)
    {
        Head = source._head;
        Tail = source._tail;
        Size = source._size;
        Version = source._version;
        ClearArray = PooledQueue<T>.s_clearArray;
        Array = source._array;
    }
}

partial class CollectionInternalsUnsafe
{
    /// <summary>Returns a structure that holds references to internal fields of <paramref name="source"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QueueInternalsRefUnsafe<T> GetRef<T>(PooledQueue<T> source) => new(source);

    /// <summary>Returns the internal array as a <see cref="Span{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this PooledQueue<T> source, out int head, out int tail)
    {
        head = source._head;
        tail = source._tail;
        return source._array.AsSpan(0, source._size);
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