namespace Tiny.PooledCollections;

using System.Collections.Generic;
using System.Runtime.CompilerServices;

public readonly struct ArrayKeyValuePair<TKey, TValue>
{
    readonly TValue[] _values;
    readonly int _index;

    internal ArrayKeyValuePair(TKey keys, TValue[] values, int index)
    {
        _values = values;
        _index = index;
        Key = keys;
    }

    public TKey Key
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    public ref TValue Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _values[_index];
    }

    public void Deconstruct(out TKey key, out TValue value)
    {
        key = Key;
        value = _values[_index];
    }

    public static implicit operator KeyValuePair<TKey, TValue>(in ArrayKeyValuePair<TKey, TValue> kvp)
        => new(kvp.Key, kvp.Value);
}
