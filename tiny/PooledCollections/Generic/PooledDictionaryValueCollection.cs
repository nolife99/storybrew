// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/Dictionary.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct PooledDictionaryValueCollection<TKey, TValue> : ICollection<TValue>, IReadOnlyCollection<TValue>
{
    readonly PooledDictionary<TKey, TValue> _dictionary;

    internal PooledDictionaryValueCollection(PooledDictionary<TKey, TValue> dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        _dictionary = dictionary;
    }

    public Enumerator GetEnumerator() => new(_dictionary);

    public void CopyTo(TValue[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);

        if ((uint)arrayIndex > array.Length) ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();

        if (array.Length - arrayIndex < _dictionary.Count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);

        var count = _dictionary._count;
        var entries = _dictionary._entries;
        for (var i = 0; i < count; i++)
            if (entries![i].Next >= -1)
                array[arrayIndex++] = entries[i].Value;
    }

    public int Count => _dictionary.Count;

    bool ICollection<TValue>.IsReadOnly => true;

    void ICollection<TValue>.Add(TValue item)
        => ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_ValueCollectionSet);

    bool ICollection<TValue>.Remove(TValue item)
    {
        ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_ValueCollectionSet);
        return false;
    }

    void ICollection<TValue>.Clear()
        => ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_ValueCollectionSet);

    bool ICollection<TValue>.Contains(TValue item) => _dictionary.ContainsValue(item);

    IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => new Enumerator(_dictionary);

    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_dictionary);

    public struct Enumerator : IEnumerator<TValue>
    {
        readonly PooledDictionary<TKey, TValue> _dictionary;
        int _index;
        readonly int _version;

        internal Enumerator(PooledDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            _version = dictionary._version;
            _index = 0;
            Current = default;
        }

        public void Dispose() { }

        public bool MoveNext()
        {
            if (_version != _dictionary._version)
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

            ref var entry = ref MemoryMarshal.GetArrayDataReference(_dictionary._entries!);
            while ((uint)_index < (uint)_dictionary._count)
            {
                ref var localEntry = ref Unsafe.Add(ref entry, _index++);

                if (localEntry.Next < -1) continue;

                Current = localEntry.Value;
                return true;
            }

            _index = _dictionary._count + 1;
            Current = default;
            return false;
        }

        public TValue Current { get; private set; }

        object IEnumerator.Current
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