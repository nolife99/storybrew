using System;
using System.Collections.Generic;

namespace Tiny
{
    public class TinyObject : TinyToken, IEnumerable<KeyValuePair<string, TinyToken>>
    {
        readonly Dictionary<string, int> keyToIndexMap = new();
        readonly IList<KeyValuePair<string, TinyToken>> items = new List<KeyValuePair<string, TinyToken>>();

        public override bool IsInline => false;
        public override bool IsEmpty => items.Count == 0;
        public override TinyTokenType Type => TinyTokenType.Object;

        public TinyToken this[string key]
        {
            get
            {
                if (keyToIndexMap.TryGetValue(key, out int index)) return items[index].Value;
                else return null;
            }
            set
            {
                if (keyToIndexMap.TryGetValue(key, out int index)) items[index] = new KeyValuePair<string, TinyToken>(key, value);
                else Add(key, value);
            }
        }

        public int Count => items.Count;

        public void Add(string key, object value) => Add(key, ToToken(value));
        public void Add(string key, TinyToken value)
        {
            items.Add(new KeyValuePair<string, TinyToken>(key, value));
            keyToIndexMap[key] = items.Count - 1;
        }

        public void Add(KeyValuePair<string, TinyToken> item) => Add(item.Key, item.Value);

        public bool TryGetValue(string key, out TinyToken value)
        {
            if (keyToIndexMap.TryGetValue(key, out int index))
            {
                value = items[index].Value;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public IEnumerator<KeyValuePair<string, TinyToken>> GetEnumerator() => items.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => items.GetEnumerator();

        public override T Value<T>(object key)
        {
            if (key == null) return (T)(object)this;

            if (key is string k)
            {
                if (keyToIndexMap.TryGetValue(k, out int index)) return items[index].Value.Value<T>();
                else return default;
            }
            else if (key is int index) return items[index].Value.Value<T>();

            throw new ArgumentException($"Key must be an integer or a string, was {key}", "key");
        }

        public override string ToString() => string.Join(", ", items);

        public bool Remove(string key)
        {
            if (keyToIndexMap.TryGetValue(key, out int index))
            {
                items.RemoveAt(index);
                keyToIndexMap.Remove(key);

                for (var i = index; i < items.Count; ++i)
                {
                    var currentKey = items[i].Key;
                    keyToIndexMap[currentKey] = i;
                }
                return true;
            }
            return false;
        }
    }
}