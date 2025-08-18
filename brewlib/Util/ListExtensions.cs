namespace BrewLib.Util;

using System.Collections.Generic;

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
}