namespace Tiny.PooledCollections.Generic.Extensions;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Tiny.PooledCollections.Generic.Temporary;

public static class TempListExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Extensions<T> GetExtensions<T>(this scoped ref readonly TempList<T> self) => new(in self);

    public readonly ref struct Extensions<T>
    {
        readonly TempList<T> _list;

        internal Extensions(scoped ref readonly TempList<T> list) => _list = list;

        public void ConvertAll<TOut, TOutput>(TOutput output, Converter<T, TOut> converter) where TOutput : ICollection<TOut>
        {
            ArgumentNullException.ThrowIfNull(converter);

            ArgumentNullException.ThrowIfNull(output);

            var items = _list._items;

            for (var i = 0; i < _list._size; i++) output.Add(converter(items[i]));
        }

        public void FindAll<TOutput>(TOutput output, Predicate<T> match) where TOutput : ICollection<T>
        {
            ArgumentNullException.ThrowIfNull(match);

            ArgumentNullException.ThrowIfNull(output);

            var items = _list._items;

            for (var i = 0; i < _list._size; i++)
                if (match(items[i]))
                    output.Add(items[i]);
        }
    }
}