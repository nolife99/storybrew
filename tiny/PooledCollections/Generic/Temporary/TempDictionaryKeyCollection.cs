// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/Dictionary.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable CS8632

namespace Tiny.PooledCollections.Generic.Temporary;

public readonly ref struct TempDictionaryKeyCollection<TKey, TValue>
{
    readonly TempDictionary<TKey, TValue> _dictionary;

    public TempDictionaryKeyCollection(TempDictionary<TKey, TValue> dictionary) => _dictionary = dictionary;

    public Enumerator GetEnumerator() => new(_dictionary);

    public void CopyTo(TKey[] array, int index)
    {
        if (array == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);

        if (index < 0 || index > array.Length) ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();

        if (array.Length - index < _dictionary.Count)
            ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);

        var count = _dictionary._count;
        var entries = _dictionary._entries;
        for (var i = 0; i < count; i++)
            if (entries![i].Next >= -1)
                array[index++] = entries[i].Key;
    }

    public int Count => _dictionary.Count;

    public ref struct Enumerator
    {
        readonly TempDictionary<TKey, TValue> _dictionary;
        int _index;
        readonly int _version;

        public Enumerator(TempDictionary<TKey, TValue> dictionary)
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
    }
}