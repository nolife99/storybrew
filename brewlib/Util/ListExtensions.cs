using System;
using System.Collections.Generic;

namespace BrewLib.Util;

public static class ListExtensions
{
    public static void Move<T>(this List<T> list, int from, int to)
    {
        if (from == to) return;

        var item = list[from];
        if (from < to) for (var i = from; i < to; ++i) list[i] = list[i + 1];
        else for (var i = from; i > to; --i) list[i] = list[i - 1];
        list[to] = item;
    }
    public static void ForEach<T>(this List<T> list, Action<T> action, Func<T, bool> condition) => list.ForEach(item =>
    {
        if (condition(item)) action(item);
    });
    public static void Dispose<TKey, TValue>(this IDictionary<TKey, TValue> disposable) where TValue : IDisposable
    {
        foreach (var reference in disposable)
        {
            reference.Value?.Dispose();
        }
        disposable.Clear();
    }
}