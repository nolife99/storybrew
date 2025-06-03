namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Runtime.CompilerServices;

public readonly ref struct QueueInternalsRef<T>
{
    [NonSerialized] public readonly int Head;
    [NonSerialized] public readonly int Tail;
    [NonSerialized] public readonly int Size;
    [NonSerialized] public readonly int Version;
    [NonSerialized] public readonly bool ClearArray;
    [NonSerialized] public readonly ReadOnlySpan<T> Array;

    public QueueInternalsRef(PooledQueue<T> source)
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
    /// <summary>Returns a structure that holds references to internal fields of <paramref name="source"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QueueInternalsRef<T> GetRef<T>(PooledQueue<T> source) => new(source);

    /// <summary>Returns the internal array as a <see cref="ReadOnlySpan{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this PooledQueue<T> source, out int head, out int tail)
    {
        head = source._head;
        tail = source._tail;
        return source._array.AsSpan(0, source._size);
    }

    /// <summary>Returns the internal array as a <see cref="ReadOnlyMemory{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this PooledQueue<T> source, out int head, out int tail)
    {
        head = source._head;
        tail = source._tail;
        return source._array.AsMemory(0, source._size);
    }
}