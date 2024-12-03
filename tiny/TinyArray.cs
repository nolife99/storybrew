namespace Tiny;

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;

public class TinyArray : TinyToken, IList<TinyToken>
{
    TinyToken[] tokens;

    public TinyArray()
    {
        const int InitialCapacity = 4;
        tokens = ArrayPool<TinyToken>.Shared.Rent(InitialCapacity);
        Count = 0;
    }

    public TinyArray(IEnumerable values) : this()
    {
        foreach (var value in values) Add(ToToken(value));
    }

    public override bool IsInline => false;
    public override bool IsEmpty => Count == 0;
    public override TinyTokenType Type => TinyTokenType.Array;

    public TinyToken this[int index] { get => tokens[index]; set => tokens[index] = value; }

    public int Count { get; private set; }

    public bool IsReadOnly => false;

    public void Add(TinyToken item)
    {
        if (Count == tokens.Length) EnsureCapacity(Count * 2);
        tokens[Count++] = item;
    }

    public void Clear()
    {
        Array.Clear(tokens, 0, Count);
        Count = 0;
    }

    public bool Contains(TinyToken item)
    {
        for (var i = 0; i < Count; ++i)
            if (tokens[i] == item)
                return true;

        return false;
    }

    public void CopyTo(TinyToken[] array, int arrayIndex) => Array.Copy(tokens, 0, array, arrayIndex, Count);

    public int IndexOf(TinyToken item)
    {
        for (var i = 0; i < Count; ++i)
            if (tokens[i] == item)
                return i;

        return -1;
    }

    public void Insert(int index, TinyToken item)
    {
        if (Count == tokens.Length) EnsureCapacity(Count * 2);

        Array.Copy(tokens, index, tokens, index + 1, Count - index);
        tokens[index] = item;
        ++Count;
    }

    public bool Remove(TinyToken item)
    {
        var index = IndexOf(item);
        if (index < 0) return false;

        RemoveAt(index);
        return true;
    }

    public void RemoveAt(int index)
    {
        Array.Copy(tokens, index + 1, tokens, index, Count - index - 1);
        --Count;
    }

    public IEnumerator<TinyToken> GetEnumerator()
    {
        for (var i = 0; i < Count; ++i) yield return tokens[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => tokens.GetEnumerator();

    public override T Value<T>(object key) => key switch
    {
        null => (T)(object)this,
        int index => this[index].Value<T>(),
        _ => throw new ArgumentException($"Key must be an integer, was {key}", nameof(key))
    };

    public override string ToString() => string.Join(", ", tokens, 0, Count);

    void EnsureCapacity(int capacity)
    {
        if (tokens.Length >= capacity) return;

        var newTokens = ArrayPool<TinyToken>.Shared.Rent(Math.Max(capacity, tokens.Length * 2));
        Array.Copy(tokens, newTokens, Count);

        ArrayPool<TinyToken>.Shared.Return(tokens);
        tokens = newTokens;
    }
}