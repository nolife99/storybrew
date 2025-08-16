namespace BrewLib.Util;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static class ListExtensions
{
    public static void Move<T>(this IList<T> list, int from, int to)
    {
        if (from == to) return;

        var item = list[from];
        if (from < to)
            for (var i = from; i < to; ++i)
                list[i] = list[i + 1];
        else
            for (var i = from; i > to; --i)
                list[i] = list[i - 1];

        list[to] = item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArraySegment<T> GetArraySegment<T>(this List<T> list)
    {
        var debugView = Unsafe.As<ListDebugView<T>>(list);
        return new(debugView._items, 0, debugView._size);
    }

    class ListDebugView<T>
    {
        public T[] _items;
        public int _size;
    }
}