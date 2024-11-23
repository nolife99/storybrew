namespace Tiny;

using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class TinyTokenExtensions
{
    public static IEnumerable<T> Values<T>(this TinyToken token, object key = null) 
        => token.Value<TinyArray>(key).Select(t => t.Value<T>());

    public static T Value<T>(this TinyToken token, object key1, object key2) => token.Value<TinyToken>(key1).Value<T>(key2);

    public static void Merge(this TinyToken into, TinyToken token)
    {
        switch (token)
        {
            case TinyObject tinyObject when into is TinyObject intoObject:
            {
                foreach (var entry in tinyObject)
                {
                    var existing = intoObject.Value<TinyToken>(entry.Key);
                    if (existing is not null) existing.Merge(entry.Value);
                    else intoObject.Add(entry);
                }

                break;
            }
            case TinyArray tinyArray when into is TinyArray intoArray:
            {
                foreach (var t in tinyArray) intoArray.Add(t);

                break;
            }
            default: throw new InvalidDataException($"Cannot merge {token} into {into}");
        }
    }
}