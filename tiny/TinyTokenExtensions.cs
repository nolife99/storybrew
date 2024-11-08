namespace Tiny;

using System.Collections.Generic;
using System.IO;

public static class TinyTokenExtensions
{
    public static IEnumerable<T> Values<T>(this IEnumerable<TinyToken> tokens)
    {
        foreach (var token in tokens) yield return token.Value<T>();
    }

    public static IEnumerable<T> Values<T>(this TinyToken token, object key = null)
        => token.Value<TinyArray>(key).Values<T>();

    public static T Value<T>(this TinyToken token, object key1, object key2)
        => token.Value<TinyToken>(key1).Value<T>(key2);

    public static T Value<T>(this TinyToken token, object key1, object key2, object key3)
        => token.Value<TinyToken>(key1).Value<TinyToken>(key2).Value<T>(key3);

    public static T Value<T>(this TinyToken token, params object[] keys)
    {
        for (var i = 0; i < keys.Length - 1; ++i) token = token.Value<TinyToken>(keys[i]);
        return token.Value<T>(keys[^1]);
    }

    public static void Merge(this TinyToken into, TinyToken token)
    {
        if (token is TinyObject tinyObject && into is TinyObject intoObject)
            foreach (var entry in tinyObject)
            {
                var existing = intoObject.Value<TinyToken>(entry.Key);
                if (existing is not null)
                    existing.Merge(entry.Value);
                else
                    intoObject.Add(entry);
            }
        else if (token is TinyArray tinyArray && into is TinyArray intoArray)
            for (var i = 0; i < tinyArray.Count; ++i)
                intoArray.Add(tinyArray[i]);
        else
            throw new InvalidDataException($"Cannot merge {token} into {into}");
    }
}