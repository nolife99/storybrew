// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

partial class PooledDictionary<TKey, TValue>
{
    public AlternateLookup<TAlternateKey> GetAlternateLookup<TAlternateKey>()
        where TAlternateKey : notnull, allows ref struct
    {
        if (!AlternateLookup<TAlternateKey>.IsCompatibleKey(this))
            throw new InvalidOperationException("Incompatible comparer");

        return new AlternateLookup<TAlternateKey>(this);
    }

    public bool TryGetAlternateLookup<TAlternateKey>(out AlternateLookup<TAlternateKey> lookup)
        where TAlternateKey : notnull, allows ref struct
    {
        if (AlternateLookup<TAlternateKey>.IsCompatibleKey(this))
        {
            lookup = new AlternateLookup<TAlternateKey>(this);
            return true;
        }

        lookup = default;
        return false;
    }

    public readonly struct AlternateLookup<TAlternateKey> where TAlternateKey : notnull, allows ref struct
    {
        internal AlternateLookup(PooledDictionary<TKey, TValue> dictionary) => Dictionary = dictionary;

        public PooledDictionary<TKey, TValue> Dictionary { get; }

        public TValue this[TAlternateKey key]
        {
            get
            {
                ref var value = ref FindValue(key, out _);
                if (Unsafe.IsNullRef(ref value)) throw new KeyNotFoundException();

                return value;
            }
            set => GetValueRefOrAddDefault(key, out _) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsCompatibleKey(PooledDictionary<TKey, TValue> dictionary)
            => dictionary._comparer is IAlternateEqualityComparer<TAlternateKey, TKey>;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static IAlternateEqualityComparer<TAlternateKey, TKey> GetAlternateComparer(
            PooledDictionary<TKey, TValue> dictionary)
            => Unsafe.As<IAlternateEqualityComparer<TAlternateKey, TKey>>(dictionary._comparer);

        public bool TryGetValue(TAlternateKey key, [MaybeNullWhen(false)] out TValue value)
        {
            ref var valueRef = ref FindValue(key, out _);
            if (!Unsafe.IsNullRef(ref valueRef))
            {
                value = valueRef;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetValue(TAlternateKey key,
            [MaybeNullWhen(false)] out TKey actualKey,
            [MaybeNullWhen(false)] out TValue value)
        {
            ref var valueRef = ref FindValue(key, out actualKey);
            if (!Unsafe.IsNullRef(ref valueRef))
            {
                value = valueRef;
                return true;
            }

            value = default;
            return false;
        }

        public bool ContainsKey(TAlternateKey key) => !Unsafe.IsNullRef(ref FindValue(key, out _));

        internal ref TValue FindValue(TAlternateKey key, [MaybeNullWhen(false)] out TKey actualKey)
        {
            var dictionary = Dictionary;
            var comparer = GetAlternateComparer(dictionary);

            ref var entry = ref Unsafe.NullRef<Entry<TKey, TValue>>();
            if (!dictionary._buckets.IsNullOrEmpty())
            {
                var hashCode = (uint)comparer.GetHashCode(key);
                var i = dictionary.GetBucket(hashCode);
                var entries = dictionary._entries;
                uint collisionCount = 0;
                i--;
                do
                {
                    if ((uint)i >= (uint)entries.Length) goto ReturnNotFound;

                    entry = ref entries[i];
                    if (entry.HashCode == hashCode && comparer.Equals(key, entry.Key)) goto ReturnFound;

                    i = entry.Next;

                    collisionCount++;
                }
                while (collisionCount <= (uint)entries.Length);

                goto ConcurrentOperation;
            }

            goto ReturnNotFound;

        ConcurrentOperation:
            ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
        ReturnFound:
            ref var value = ref entry.Value;
            actualKey = entry.Key;
        Return:
            return ref value;

        ReturnNotFound:
            value = ref Unsafe.NullRef<TValue>();
            actualKey = default!;
            goto Return;
        }

        public bool Remove(TAlternateKey key) => Remove(key, out _, out _);

        public bool Remove(TAlternateKey key,
            [MaybeNullWhen(false)] out TKey actualKey,
            [MaybeNullWhen(false)] out TValue value)
        {
            var dictionary = Dictionary;
            var comparer = GetAlternateComparer(dictionary);

            if (!dictionary._buckets.IsNullOrEmpty())
            {
                uint collisionCount = 0;

                var hashCode = (uint)comparer.GetHashCode(key);

                ref var bucket = ref dictionary.GetBucket(hashCode);
                var entries = dictionary._entries;
                var last = -1;
                var i = bucket - 1;
                while (i >= 0)
                {
                    ref var entry = ref entries[i];

                    if (entry.HashCode == hashCode && comparer.Equals(key, entry.Key))
                    {
                        if (last < 0) bucket = entry.Next + 1;
                        else entries[last].Next = entry.Next;

                        actualKey = entry.Key;
                        value = entry.Value;

                        entry.Next = StartOfFreeList - dictionary._freeList;

                        if (RuntimeHelpers.IsReferenceOrContainsReferences<TKey>()) entry.Key = default!;

                        if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>()) entry.Value = default;

                        dictionary._freeList = i;
                        dictionary._freeCount++;
                        return true;
                    }

                    last = i;
                    i = entry.Next;

                    collisionCount++;
                    if (collisionCount > (uint)entries.Length)
                        ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }
            }

            actualKey = default;
            value = default;
            return false;
        }

        public bool TryAdd(TAlternateKey key, TValue value)
        {
            ref var slot = ref GetValueRefOrAddDefault(key, out var exists);
            if (!exists)
            {
                slot = value;
                return true;
            }

            return false;
        }

        internal ref TValue GetValueRefOrAddDefault(TAlternateKey key, out bool exists)
        {
            var dictionary = Dictionary;
            var comparer = GetAlternateComparer(dictionary);

            if (dictionary._buckets.IsNullOrEmpty()) dictionary.Initialize(0);

            var entries = dictionary._entries;

            var hashCode = (uint)comparer.GetHashCode(key);

            uint collisionCount = 0;
            ref var bucket = ref dictionary.GetBucket(hashCode);
            var i = bucket - 1;

            while ((uint)i < (uint)entries.Length)
            {
                if (entries[i].HashCode == hashCode && comparer.Equals(key, entries[i].Key))
                {
                    exists = true;

                    return ref entries[i].Value!;
                }

                i = entries[i].Next;

                collisionCount++;
                if (collisionCount > (uint)entries.Length)
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
            }

            var actualKey = comparer.Create(key);
            ArgumentNullException.ThrowIfNull(actualKey, nameof(key));

            int index;
            if (dictionary._freeCount > 0)
            {
                index = dictionary._freeList;

                dictionary._freeList = StartOfFreeList - entries[dictionary._freeList].Next;
                dictionary._freeCount--;
            }
            else
            {
                var count = dictionary._count;
                if (count == entries.Length)
                {
                    dictionary.Resize();
                    bucket = ref dictionary.GetBucket(hashCode);
                }

                index = count;
                dictionary._count = count + 1;
                entries = dictionary._entries;
            }

            ref var entry = ref entries![index];
            entry.HashCode = hashCode;
            entry.Next = bucket - 1;
            entry.Key = actualKey;
            entry.Value = default;
            bucket = index + 1;
            dictionary._version++;

            if (!typeof(TKey).IsValueType &&
                collisionCount > HashHelpers.HashCollisionThreshold &&
                ReferenceEquals(comparer, _stringComparer))
            {
                dictionary.Resize(entries.Length, true);

                exists = false;

                ref var value = ref dictionary.FindValue(actualKey)!;

                return ref value;
            }

            exists = false;

            return ref entry.Value!;
        }
    }
}