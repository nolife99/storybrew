using System;
using System.Collections;
using System.Collections.Generic;

namespace Tiny
{
    public class TinyArray : TinyToken, IList<TinyToken>
    {
        TinyToken[] tokens;
        int count;

        public override bool IsInline => false;
        public override bool IsEmpty => count == 0;
        public override TinyTokenType Type => TinyTokenType.Array;

        public TinyToken this[int index]
        {
            get => tokens[index];
            set => tokens[index] = value;
        }

        public TinyArray()
        {
            const int InitialCapacity = 4;
            tokens = new TinyToken[InitialCapacity];
            count = 0;
        }

        public TinyArray(IEnumerable values)
        {
            const int InitialCapacity = 4;
            tokens = new TinyToken[InitialCapacity];
            count = 0;

            foreach (var value in values) Add(ToToken(value));
        }

        public int Count => count;
        public bool IsReadOnly => false;

        public void Add(TinyToken item)
        {
            if (count == tokens.Length) EnsureCapacity(count * 2);
            tokens[count++] = item;
        }

        public void Clear()
        {
            Array.Clear(tokens, 0, count);
            count = 0;
        }

        public bool Contains(TinyToken item)
        {
            for (var i = 0; i < count; ++i) if (tokens[i] == item) return true;
            return false;
        }

        public void CopyTo(TinyToken[] array, int arrayIndex) => Array.Copy(tokens, 0, array, arrayIndex, count);

        public int IndexOf(TinyToken item)
        {
            for (var i = 0; i < count; ++i) if (tokens[i] == item) return i;
            return -1;
        }

        public void Insert(int index, TinyToken item)
        {
            if (count == tokens.Length) EnsureCapacity(count * 2);

            Array.Copy(tokens, index, tokens, index + 1, count - index);
            tokens[index] = item;
            ++count;
        }

        public bool Remove(TinyToken item)
        {
            int index = IndexOf(item);
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }
            return false;
        }

        public void RemoveAt(int index)
        {
            Array.Copy(tokens, index + 1, tokens, index, count - index - 1);
            --count;
        }

        public IEnumerator<TinyToken> GetEnumerator()
        {
            for (var i = 0; i < count; ++i) yield return tokens[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => tokens.GetEnumerator();

        public override T Value<T>(object key)
        {
            if (key is null) return (T)(object)this;
            if (key is int index) return this[index].Value<T>();

            throw new ArgumentException($"Key must be an integer, was {key}", nameof(key));
        }

        public override string ToString() => string.Join(", ", tokens, 0, count);

        void EnsureCapacity(int capacity)
        {
            if (tokens.Length >= capacity) return;

            var newTokens = new TinyToken[Math.Max(capacity, tokens.Length * 2)];
            Array.Copy(tokens, newTokens, count);
            tokens = newTokens;
        }
    }
}