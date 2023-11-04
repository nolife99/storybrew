using System;
using System.Collections.Generic;

namespace BrewLib.Util
{
    public static class ListExtensions
    {
        public static void Move<T>(this List<T> list, int from, int to)
        {
            if (from == to) return;

            var item = list[from];
            if (from < to) for (var index = from; index < to; ++index) list[index] = list[index + 1];
            else for (var index = from; index > to; --index) list[index] = list[index - 1];
            list[to] = item;
        }
        public static bool ForEach<T>(this List<T> list, Action<T> action, Func<T, bool> condition)
        {
            try
            {
                list.TrimExcess();
                for (var i = 0; i < list.Count; ++i) if (condition(list[i])) action(list[i]);
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}