namespace Tiny.PooledCollections.Generic.StructBased;

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

partial struct ValueArrayDictionary<TKey, TValue>
{
    public struct Enumerator : IEnumerator<ArrayKVPair<TKey, TValue>>
    {
        readonly ValueArrayDictionary<TKey, TValue> _dictionary;

#if DEBUG
        private int _startCount;
#endif

        int _count;
        int _index;

        public Enumerator(in ValueArrayDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            _index = -1;
            _count = dictionary.Count;
#if DEBUG
            _startCount = dictionary.Count;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
#if DEBUG
            if (_count != _startCount) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
#endif
            if (_index < _count - 1)
            {
                ++_index;
                return true;
            }

            return false;
        }

        public ArrayKVPair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(_dictionary._entries[_index].Key, _dictionary._values, _index);
        }

        object IEnumerator.Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new ArrayKVPair<TKey, TValue>(_dictionary._entries[_index].Key, _dictionary._values, _index);
        }

        public void SetRange(int startIndex, int count)
        {
            _index = startIndex - 1;
            _count = count;
#if DEBUG
            if (_count > _startCount) throw new InvalidOperationException("Cannot set a count greater than its starting value");

            _startCount = count;
#endif
        }

        public void Reset() => _index = -1;

        public void Dispose() { }
    }

    struct KeyValuePairEnumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        readonly ValueArrayDictionary<TKey, TValue> _dictionary;

#if DEBUG
        private int _startCount;
#endif

        readonly int _count;
        int _index;

        public KeyValuePairEnumerator(in ValueArrayDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            _index = -1;
            _count = dictionary.Count;
#if DEBUG
            _startCount = dictionary.Count;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
#if DEBUG
            if (_count != _startCount) ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
#endif
            if (_index < _count - 1)
            {
                ++_index;
                return true;
            }

            return false;
        }

        public KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(_dictionary._entries[_index].Key, _dictionary._values[_index]);
        }

        object IEnumerator.Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new KeyValuePair<TKey, TValue>(_dictionary._entries[_index].Key, _dictionary._values[_index]);
        }

        public void Reset() => _index = -1;

        public void Dispose() { }
    }
}