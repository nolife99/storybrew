namespace BrewLib.Util
{
    using System;
    using System.Collections.Generic;

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
        public static bool ForEach<T>(this List<T> list, Action<T> action, Predicate<T> condition)
        {
            try
            {
                list.ForEach(obj =>
                {
                    if (condition(obj)) action(obj);
                });
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}