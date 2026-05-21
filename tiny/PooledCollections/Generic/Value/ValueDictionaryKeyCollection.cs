// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/Dictionary.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic.Value;

using System;
using System.Collections;
using System.Collections.Generic;

public readonly struct ValueDictionaryKeyCollection<TKey, TValue> : ICollection<TKey>, IReadOnlyCollection<TKey>
{
    readonly ValueDictionary<TKey, TValue> _dictionary;

    internal ValueDictionaryKeyCollection(ValueDictionary<TKey, TValue> dictionary) => _dictionary = dictionary;

    public Enumerator GetEnumerator() => new(in _dictionary);

    public void CopyTo(TKey[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (arrayIndex < 0 || arrayIndex > array.Length)
            ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();

        if (array.Length - arrayIndex < _dictionary.Count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);

        var count = _dictionary._count;
        var entries = _dictionary._entries;
        for (var i = 0; i < count; i++)
            if (entries![i].Next >= -1)
                array[arrayIndex++] = entries[i].Key;
    }

    public int Count => _dictionary.Count;

    bool ICollection<TKey>.IsReadOnly => true;

    void ICollection<TKey>.Add(TKey item)
        => ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_KeyCollectionSet);

    void ICollection<TKey>.Clear()
        => ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_KeyCollectionSet);

    bool ICollection<TKey>.Contains(TKey item) => _dictionary.ContainsKey(item);

    bool ICollection<TKey>.Remove(TKey item)
    {
        ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_KeyCollectionSet);
        return false;
    }

    IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => new Enumerator(in _dictionary);

    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(in _dictionary);

    public struct Enumerator : IEnumerator<TKey>
    {
        readonly ValueDictionary<TKey, TValue> _dictionary;
        int _index;
        readonly int _version;

        public Enumerator(scoped ref readonly ValueDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            _version = dictionary._version;
            _index = 0;
            Current = default;
        }

        public readonly void Dispose() { }

        public bool MoveNext()
        {
            if (_version != _dictionary._version)
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            while ((uint)_index < (uint)_dictionary._count)
            {
                ref var entry = ref _dictionary._entries![_index++];

                if (entry.Next >= -1)
                {
                    Current = entry.Key;
                    return true;
                }
            }

            _index = _dictionary._count + 1;
            Current = default;
            return false;
        }

        public TKey Current { get; private set; }

        readonly object IEnumerator.Current
        {
            get
            {
                if (_index == 0 || _index == _dictionary._count + 1)
                    ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();

                return Current;
            }
        }

        void IEnumerator.Reset()
        {
            if (_version != _dictionary._version)
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            _index = 0;
            Current = default;
        }
    }
}
