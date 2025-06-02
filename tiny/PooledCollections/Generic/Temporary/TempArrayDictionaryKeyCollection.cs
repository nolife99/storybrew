namespace Tiny.PooledCollections.Generic.Temporary;

using System;
using System.Runtime.CompilerServices;

public readonly ref struct TempArrayDictionaryKeyCollection<TKey, TValue>
{
    readonly TempArrayDictionary<TKey, TValue> _dictionary;

    internal TempArrayDictionaryKeyCollection(TempArrayDictionary<TKey, TValue> dictionary) => _dictionary = dictionary;

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _dictionary.Count;
    }

    public bool IsReadOnly => true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(TKey item) => _dictionary.ContainsKey(item);

    public void CopyTo(TKey[] dest, int destIndex)
    {
        if (destIndex < 0 || destIndex > dest.Length)
            ThrowHelper.ThrowDestIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual();

        if (dest.Length - destIndex < Count) ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);

        var keys = _dictionary._entries.AsSpan();

        if (keys.Length == 0) return;

        for (int i = 0, len = _dictionary.Count; i < len; i++) dest[destIndex++] = keys[i].Key;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_dictionary);

    public ref struct Enumerator(TempArrayDictionary<TKey, TValue> dictionary)
    {
        readonly TempArrayDictionary<TKey, TValue> _dictionary = dictionary;
        readonly int _count = dictionary.Count;

        int _index = -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
#if DEBUG
            if (_count != _dictionary.Count) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
#endif
            if (_index >= _count - 1) return false;

            ++_index;
            return true;
        }

        public TKey Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dictionary._entries[_index].Key;
        }

        public void Reset() => _index = -1;

        public void Dispose() { }
    }
}