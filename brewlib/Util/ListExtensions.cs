namespace BrewLib.Util;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class ListExtensions
{
    public static void Move<T>(this List<T> list, int from, int to)
    {
        if (from == to) return;
        var span = CollectionsMarshal.AsSpan(list);

        var item = span[from];
        if (from < to)
            for (var i = from; i < to; ++i)
                span[i] = span[i + 1];
        else
            for (var i = from; i > to; --i)
                span[i] = span[i - 1];

        span[to] = item;
    }

    public static void Dispose<TKey, TValue>(this Dictionary<TKey, TValue> disposable) where TValue : IDisposable
    {
        foreach (var reference in disposable.Values) reference?.Dispose();
    }
}