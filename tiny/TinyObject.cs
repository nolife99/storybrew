namespace Tiny;

using System;
using System.Collections;
using System.Collections.Generic;

public class TinyObject : TinyToken, IEnumerable<KeyValuePair<string, TinyToken>>
{
    readonly List<KeyValuePair<string, TinyToken>> items = [];
    readonly Dictionary<string, int> keyToIndexMap = [];

    public override bool IsInline => false;
    public override bool IsEmpty => items.Count == 0;
    public override TinyTokenType Type => TinyTokenType.Object;

    public TinyToken this[string key]
    {
        get => keyToIndexMap.TryGetValue(key, out var index) ? items[index].Value : null;
        set
        {
            if (keyToIndexMap.TryGetValue(key, out var index))
                items[index] = new KeyValuePair<string, TinyToken>(key, value);
            else
                Add(key, value);
        }
    }

    public int Count => items.Count;

    public IEnumerator<KeyValuePair<string, TinyToken>> GetEnumerator() => items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();

    public void Add(string key, object value) => Add(key, ToToken(value));

    public void Add(string key, TinyToken value)
    {
        items.Add(new(key, value));
        keyToIndexMap[key] = items.Count - 1;
    }

    public void Add(KeyValuePair<string, TinyToken> item) => Add(item.Key, item.Value);

    public override T Value<T>(object key)
        => key switch
        {
            null => (T)(object)this,
            string k when keyToIndexMap.TryGetValue(k, out var index) => items[index].Value.Value<T>(),
            string k => default, int index => items[index].Value.Value<T>(),
            _ => throw new ArgumentException($"Key must be an integer or a string, was {key}", nameof(key))
        };

    public override string ToString() => string.Join(", ", items);
}