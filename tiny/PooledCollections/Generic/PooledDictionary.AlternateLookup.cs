namespace Tiny.PooledCollections.Generic;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

    /// <summary>
    ///     Provides a type that may be used to perform operations on a <see cref="Dictionary{TKey,TValue}"/> using a
    ///     <typeparamref name="TAlternateKey"/> as a key instead of a <typeparamref name="TKey"/>.
    /// </summary>
    /// <typeparam name="TAlternateKey">The alternate type of a key for performing lookups.</typeparam>
    public readonly struct AlternateLookup<TAlternateKey> where TAlternateKey : notnull, allows ref struct
    {
        /// <summary>Initialize the instance. The dictionary must have already been verified to have a compatible comparer.</summary>
        internal AlternateLookup(PooledDictionary<TKey, TValue> dictionary)
        {
            Debug.Assert(dictionary is not null);
            Debug.Assert(IsCompatibleKey(dictionary));
            Dictionary = dictionary;
        }

        /// <summary>Gets the <see cref="Dictionary{TKey, TValue}"/> against which this instance performs operations.</summary>
        public PooledDictionary<TKey, TValue> Dictionary { get; }

        /// <summary>Gets or sets the value associated with the specified alternate key.</summary>
        /// <param name="key">The alternate key of the value to get or set.</param>
        /// <value>
        ///     The value associated with the specified alternate key. If the specified alternate key is not found, a get operation
        ///     throws a <see cref="KeyNotFoundException"/>, and a set operation creates a new element with the specified key.
        /// </value>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
        /// <exception cref="KeyNotFoundException">The property is retrieved and alternate key does not exist in the collection.</exception>
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

        /// <summary>Checks whether the dictionary has a comparer compatible with <typeparamref name="TAlternateKey"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsCompatibleKey(PooledDictionary<TKey, TValue> dictionary)
        {
            Debug.Assert(dictionary is not null);
            return dictionary._comparer is IAlternateEqualityComparer<TAlternateKey, TKey>;
        }

        /// <summary>Gets the dictionary's alternate comparer. The dictionary must have already been verified as compatible.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static IAlternateEqualityComparer<TAlternateKey, TKey> GetAlternateComparer(
            PooledDictionary<TKey, TValue> dictionary)
        {
            Debug.Assert(IsCompatibleKey(dictionary));
            return Unsafe.As<IAlternateEqualityComparer<TAlternateKey, TKey>>(dictionary._comparer);
        }

        /// <summary>Gets the value associated with the specified alternate key.</summary>
        /// <param name="key">The alternate key of the value to get.</param>
        /// <param name="value">
        ///     When this method returns, contains the value associated with the specified key, if the key is found;
        ///     otherwise, the default value for the type of the value parameter.
        /// </param>
        /// <returns><see langword="true"/> if an entry was found; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
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

        /// <summary>Gets the value associated with the specified alternate key.</summary>
        /// <param name="key">The alternate key of the value to get.</param>
        /// <param name="actualKey">
        ///     When this method returns, contains the actual key associated with the alternate key, if the key is
        ///     found; otherwise, the default value for the type of the key parameter.
        /// </param>
        /// <param name="value">
        ///     When this method returns, contains the value associated with the specified key, if the key is found;
        ///     otherwise, the default value for the type of the value parameter.
        /// </param>
        /// <returns><see langword="true"/> if an entry was found; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
        public bool TryGetValue(TAlternateKey key,
            [MaybeNullWhen(false)] out TKey actualKey,
            [MaybeNullWhen(false)] out TValue value)
        {
            ref var valueRef = ref FindValue(key, out actualKey);
            if (!Unsafe.IsNullRef(ref valueRef))
            {
                value = valueRef;
                Debug.Assert(actualKey is not null);
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Determines whether the <see cref="PooledDictionary{TKey, TValue}"/> contains the specified alternate key.</summary>
        /// <param name="key">The alternate key to check.</param>
        /// <returns><see langword="true"/> if the key is in the dictionary; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
        public bool ContainsKey(TAlternateKey key) => !Unsafe.IsNullRef(ref FindValue(key, out _));

        /// <summary>Finds the entry associated with the specified alternate key.</summary>
        /// <param name="key">The alternate key.</param>
        /// <param name="actualKey">The actual key, if found.</param>
        /// <returns>A reference to the value associated with the key, if found; otherwise, a null reference.</returns>
        internal ref TValue FindValue(TAlternateKey key, [MaybeNullWhen(false)] out TKey actualKey)
        {
            var dictionary = Dictionary;
            var comparer = GetAlternateComparer(dictionary);

            ref var entry = ref Unsafe.NullRef<Entry<TKey, TValue>>();
            if (!dictionary._buckets.IsNullOrEmpty())
            {
                Debug.Assert(dictionary._entries != null, "expected entries to be != null");

                var hashCode = (uint)comparer.GetHashCode(key);
                var i = dictionary.GetBucket(hashCode);
                var entries = dictionary._entries;
                uint collisionCount = 0;
                i--; // Value in _buckets is 1-based; subtract 1 from i. We do it here so it fuses with the following conditional.
                do
                {
                    // Should be a while loop https://github.com/dotnet/runtime/issues/9422
                    // Test in if to drop range check for following array access
                    if ((uint)i >= (uint)entries.Length) goto ReturnNotFound;

                    entry = ref entries[i];
                    if (entry.HashCode == hashCode && comparer.Equals(key, entry.Key)) goto ReturnFound;

                    i = entry.Next;

                    collisionCount++;
                }
                while (collisionCount <= (uint)entries.Length);

                // The chain of entries forms a loop; which means a concurrent update has happened.
                // Break out of the loop and throw, rather than looping forever.
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

        /// <summary>Removes the value with the specified alternate key from the <see cref="Dictionary{TKey, TValue}"/>.</summary>
        /// <param name="key">The alternate key of the element to remove.</param>
        /// <returns>true if the element is successfully found and removed; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
        public bool Remove(TAlternateKey key) => Remove(key, out _, out _);

        /// <summary>
        ///     Removes the value with the specified alternate key from the <see cref="Dictionary{TKey, TValue}"/>, and copies the
        ///     element to the value parameter.
        /// </summary>
        /// <param name="key">The alternate key of the element to remove.</param>
        /// <param name="actualKey">The removed key.</param>
        /// <param name="value">The removed element.</param>
        /// <returns>true if the element is successfully found and removed; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
        public bool Remove(TAlternateKey key,
            [MaybeNullWhen(false)] out TKey actualKey,
            [MaybeNullWhen(false)] out TValue value)
        {
            var dictionary = Dictionary;
            var comparer = GetAlternateComparer(dictionary);

            if (!dictionary._buckets.IsNullOrEmpty())
            {
                Debug.Assert(dictionary._entries != null, "entries should be non-null");
                uint collisionCount = 0;

                var hashCode = (uint)comparer.GetHashCode(key);

                ref var bucket = ref dictionary.GetBucket(hashCode);
                var entries = dictionary._entries;
                var last = -1;
                var i = bucket - 1; // Value in buckets is 1-based
                while (i >= 0)
                {
                    ref var entry = ref entries[i];

                    if (entry.HashCode == hashCode && comparer.Equals(key, entry.Key))
                    {
                        if (last < 0) bucket = entry.Next + 1; // Value in buckets is 1-based
                        else entries[last].Next = entry.Next;

                        actualKey = entry.Key;
                        value = entry.Value;

                        Debug.Assert(StartOfFreeList - dictionary._freeList < 0,
                            "shouldn't underflow because max hashtable length is MaxPrimeArrayLength = 0x7FEFFFFD(2146435069) _freelist underflow threshold 2147483646");

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

                        // The chain of entries forms a loop; which means a concurrent update has happened.
                        // Break out of the loop and throw, rather than looping forever.
                        ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }
            }

            actualKey = default;
            value = default;
            return false;
        }

        /// <summary>Attempts to add the specified key and value to the dictionary.</summary>
        /// <param name="key">The alternate key of the element to add.</param>
        /// <param name="value">The value of the element to add.</param>
        /// <returns>true if the key/value pair was added to the dictionary successfully; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
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

        /// <inheritdoc cref="CollectionsMarshal.GetValueRefOrAddDefault{TKey, TValue}(Dictionary{TKey, TValue}, TKey, out bool)"/>
        internal ref TValue? GetValueRefOrAddDefault(TAlternateKey key, out bool exists)
        {
            // NOTE: this method is a mirror of GetValueRefOrAddDefault above. Keep it in sync.

            var dictionary = Dictionary;
            var comparer = GetAlternateComparer(dictionary);

            if (dictionary._buckets == null) dictionary.Initialize(0);
            Debug.Assert(dictionary._buckets != null);

            var entries = dictionary._entries;
            Debug.Assert(entries != null, "expected entries to be non-null");

            var hashCode = (uint)comparer.GetHashCode(key);

            uint collisionCount = 0;
            ref var bucket = ref dictionary.GetBucket(hashCode);
            var i = bucket - 1; // Value in _buckets is 1-based

            Debug.Assert(comparer is not null);
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

                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
            }

            var actualKey = comparer.Create(key);
            ArgumentNullException.ThrowIfNull(actualKey, nameof(key));

            int index;
            if (dictionary._freeCount > 0)
            {
                index = dictionary._freeList;
                Debug.Assert(StartOfFreeList - entries[dictionary._freeList].Next >= -1,
                    "shouldn't overflow because `next` cannot underflow");

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
            entry.Next = bucket - 1; // Value in _buckets is 1-based
            entry.Key = actualKey;
            entry.Value = default;
            bucket = index + 1; // Value in _buckets is 1-based
            dictionary._version++;

            // Value types never rehash
            if (!typeof(TKey).IsValueType &&
                collisionCount > HashHelpers.HashCollisionThreshold &&
                ReferenceEquals(comparer, _stringComparer))
            {
                // If we hit the collision threshold we'll need to switch to the comparer which is using randomized string hashing
                // i.e. EqualityComparer<string>.Default.
                dictionary.Resize(entries.Length, true);

                exists = false;

                // At this point the entries array has been resized, so the current reference we have is no longer valid.
                // We're forced to do a new lookup and return an updated reference to the new entry instance. This new
                // lookup is guaranteed to always find a value though and it will never return a null reference here.
                ref var value = ref dictionary.FindValue(actualKey)!;

                Debug.Assert(!Unsafe.IsNullRef(ref value), "the lookup result cannot be a null ref here");

                return ref value;
            }

            exists = false;

            return ref entry.Value!;
        }
    }
}