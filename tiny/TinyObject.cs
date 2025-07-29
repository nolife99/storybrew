namespace Tiny;

using System;
using System.Collections;
using System.Collections.Generic;

public class TinyObject : TinyToken, IEnumerable<KeyValuePair<string, TinyToken>>
{
    readonly List<KeyValuePair<string, TinyToken>> items = [];
    readonly Dictionary<string, int> keyToIndexMap = [];
    readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> keyToIndexMapLookup;

    public TinyObject() => keyToIndexMapLookup = keyToIndexMap.GetAlternateLookup<ReadOnlySpan<char>>();

    public override bool IsInline => false;
    public override bool IsEmpty => items.Count == 0;
    public override TinyTokenType Type => TinyTokenType.Object;

    public TinyToken this[ReadOnlySpan<char> key]
    {
        get => keyToIndexMapLookup.TryGetValue(key, out var index) ? items[index].Value : null;
        set
        {
            if (keyToIndexMapLookup.TryGetValue(key, out var index)) items[index] = new(key.ToString(), value);
            else Add(key.ToString(), value);
        }
    }

    public int Count => items.Count;

    IEnumerator<KeyValuePair<string, TinyToken>> IEnumerable<KeyValuePair<string, TinyToken>>.GetEnumerator()
        => items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();

    public List<KeyValuePair<string, TinyToken>>.Enumerator GetEnumerator() => items.GetEnumerator();

    public void Add(string key, object value) => Add(key, ToToken(value));

    public void Add(string key, TinyToken value)
    {
        items.Add(new(key, value));
        keyToIndexMap[key] = items.Count - 1;
    }

    public void Add(KeyValuePair<string, TinyToken> item) => Add(item.Key, item.Value);

    public override T Value<T>(scoped ReadOnlySpan<char> key)
        => keyToIndexMapLookup.TryGetValue(key, out var index) ? items[index].Value.Value<T>() : default;

    public override T Value<T>(object key) => key switch
    {
        null => (T)(object)this,
        string k when keyToIndexMap.TryGetValue(k, out var index) => items[index].Value.Value<T>(),
        string => default,
        int index => items[index].Value.Value<T>(),
        _ => throw new ArgumentException($"Key must be an integer or a string, was {key}", nameof(key))
    };

    public override string ToString() => string.Join(", ", items);
}