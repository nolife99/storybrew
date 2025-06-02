namespace Tiny.PooledCollections.Generic.Temporary;

using System.Collections.Generic;
using System.Runtime.CompilerServices;

partial struct TempArrayDictionary<TKey, TValue>
{
    public ref struct Enumerator
    {
        readonly TempArrayDictionary<TKey, TValue> _dictionary;

#if DEBUG
        private int _startCount;
#endif

        int _count;
        int _index;

        public Enumerator(TempArrayDictionary<TKey, TValue> dictionary)
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

    ref struct KeyValuePairEnumerator
    {
        readonly TempArrayDictionary<TKey, TValue> _dictionary;

#if DEBUG
        private int _startCount;
#endif

        readonly int _count;
        int _index;

        public KeyValuePairEnumerator(in TempArrayDictionary<TKey, TValue> dictionary)
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

        public void Reset() => _index = -1;

        public void Dispose() { }
    }

    ref struct KVPairEnumerator
    {
        readonly TempArrayDictionary<TKey, TValue> _dictionary;

#if DEBUG
        private int _startCount;
#endif

        readonly int _count;
        int _index;

        public KVPairEnumerator(in TempArrayDictionary<TKey, TValue> dictionary)
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

        public KVPair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(_dictionary._entries[_index].Key, _dictionary._values[_index]);
        }

        public void Reset() => _index = -1;

        public void Dispose() { }
    }
}