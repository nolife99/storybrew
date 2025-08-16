namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Runtime.CompilerServices;

public readonly ref struct TempArrayDictionaryValueCollection<TKey, TValue>
{
    readonly TempArrayDictionary<TKey, TValue> _dictionary;

    internal TempArrayDictionaryValueCollection(TempArrayDictionary<TKey, TValue> dictionary)
        => _dictionary = dictionary;

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _dictionary.Count;
    }

    public bool IsReadOnly => true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(TValue item) => _dictionary.ContainsValue(item);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(TValue[] dest, int destIndex)
        => _dictionary._values.AsSpan(0, _dictionary.Count).CopyTo(dest.AsSpan(destIndex));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_dictionary);

    public ref struct Enumerator
    {
        readonly TempArrayDictionary<TKey, TValue> _dictionary;
        readonly int _count;

        int _index;

        public Enumerator(TempArrayDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            _index = -1;
            _count = dictionary.Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
#if DEBUG
            if (_count != _dictionary.Count) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
#endif
            if (_index < _count - 1)
            {
                ++_index;
                return true;
            }

            return false;
        }

        public TValue Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dictionary._values[_index];
        }

        public void Reset() => _index = -1;

        public void Dispose() { }
    }
}