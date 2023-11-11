using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace StorybrewCommon.Util
{
    ///<summary> Represents a unordered generic native collection of keys and values. </summary>
    ///<remarks> This collection must be released with <see cref="Clear"/> or <see cref="Dispose"/> as soon as possible. </remarks>
    ///<typeparam name="TKey"> The type of the keys in the dictionary. </typeparam>
    ///<typeparam name="TValue"> The type of the values in the dictionary. </typeparam>
    public sealed class DisposableNativeDictionary<TKey, TValue> : IDisposable, IDictionary<TKey, TValue>, IDictionary
    {
        class Node
        {
            internal TKey Key;
            internal GCHandle Handle;
            internal Node Next;

            internal Node(TKey key, GCHandle handle, Node next)
            {
                Key = key;
                Handle = handle;
                Next = next;
            }
        }

        Node[] table;
        int count;

        ///<summary> Creates a new <see cref="DisposableNativeDictionary{TKey, TValue}"/> object, with the given allocation capacity. </summary>
        public DisposableNativeDictionary(int capacity = 32) => table = new Node[capacity];

        ///<inheritdoc/>
        public TValue this[TKey key]
        {
            get
            {
                for (var node = table[getIndex(key)]; node != null; node = node.Next) if (node.Key.Equals(key))
                    return (TValue)node.Handle.Target;

                throw new KeyNotFoundException($"The specified key was not found in the dictionary: {key}");
            }
            set => Add(key, value);
        }

        ///<inheritdoc/>
        ///<exception cref="InvalidCastException"> The type of 'key' and/or 'value' cannot be casted to the respective value type. </exception>
        public object this[object key]
        {
            get
            {
                try
                {
                    for (var node = table[getIndex((TKey)key)]; node != null; node = node.Next) if (node.Key.Equals(key))
                        return (TValue)node.Handle.Target;

                    return null;
                }
                catch (InvalidCastException)
                {
                    return null;
                }
            }
            set => Add(key, value);
        }

        ///<inheritdoc/>
        public int Count => count;

        ///<inheritdoc/>
        public ICollection<TKey> Keys
        {
            get
            {
                var collection = new List<TKey>();
                for (var i = 0; i < table.Length; ++i) for (var node = table[i]; node != null; node = node.Next)
                    collection.Add(node.Key);

                return collection;
            }
        }

        ///<inheritdoc/>
        ICollection IDictionary.Keys => (ICollection)Keys;

        ///<inheritdoc/>
        public bool ContainsKey(TKey key)
        {
            for (var node = table[getIndex(key)]; node != null; node = node.Next) if (node.Key.Equals(key)) return true;
            return false;
        }

        ///<inheritdoc/>
        public bool Contains(object key) => ContainsKey((TKey)key);

        ///<inheritdoc/>
        public ICollection<TValue> Values
        {
            get
            {
                var collection = new List<TValue>();
                for (var i = 0; i < table.Length; ++i) for (var node = table[i]; node != null; node = node.Next)
                    collection.Add((TValue)node.Handle.Target);

                return collection;
            }
        }

        ///<inheritdoc/>
        ICollection IDictionary.Values => (ICollection)Values;

        ///<inheritdoc/>
        public bool Contains(KeyValuePair<TKey, TValue> item) => TryGetValue(item.Key, out TValue value) && value.Equals(item.Value);

        ///<inheritdoc cref="IDictionary.IsReadOnly"/>
        public bool IsReadOnly => false;

        ///<inheritdoc/>
        public bool IsFixedSize => false;

        object syncRoot;

        ///<inheritdoc/>
        public object SyncRoot
        {
            get
            {
                if (syncRoot is null) Interlocked.CompareExchange<object>(ref syncRoot, new object(), null);
                return syncRoot;
            }
        }

        ///<inheritdoc/>
        public bool IsSynchronized => false;

        ///<inheritdoc/>
        public bool TryGetValue(TKey key, out TValue value)
        {
            for (var node = table[getIndex(key)]; node != null; node = node.Next) if (node.Key.Equals(key))
            {
                value = (TValue)node.Handle.Target;
                return true;
            }

            value = default;
            return false;
        }

        ///<inheritdoc/>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
        {
            if (array is null) throw new ArgumentNullException(nameof(array));
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (array.Length - index < count) throw new ArgumentException("'array' does not have enough space to fit this collection's elements.");

            for (var i = 0; i < table.Length; ++i) for (var node = table[i]; node != null; node = node.Next)
                array[index++] = new KeyValuePair<TKey, TValue>(node.Key, (TValue)node.Handle.Target);
        }

        ///<inheritdoc/>
        ///<exception cref="InvalidCastException"> The type of 'array' cannot be casted to the respective value type. </exception>
        public void CopyTo(Array array, int index) => CopyTo((KeyValuePair<TKey, TValue>[])array, index);

        bool disposed;

        ///<inheritdoc/>
        public void Dispose()
        {
            if (count == 0 || table.Length == 0 || disposed) return;

            for (var i = 0; i < table.Length; ++i) for (var node = table[i]; node != null; node = node.Next)
            {
                if (node.Handle.Target is IDisposable value) value.Dispose();
                node.Handle.Free();
            }

            Array.Clear(table, 0, table.Length);
            count = 0;

            disposed = true;
        }

        int getIndex(TKey key) => (key.GetHashCode() & 0x7FFFFFFF) % table.Length;

        ///<inheritdoc/>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (var i = 0; i < table.Length; ++i) for (var node = table[i]; node != null; node = node.Next)
                yield return new KeyValuePair<TKey, TValue>(node.Key, (TValue)node.Handle.Target);
        }

        ///<inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        ///<inheritdoc/>
        public void Add(TKey key, TValue value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (count / (float)table.Length >= 1)
            {
                var oldTable = table;
                table = new Node[oldTable.Length * 2];

                for (var i = 0; i < oldTable.Length; ++i) for (var node = oldTable[i]; node != null; node = node.Next)
                {
                    var nHash = getIndex(node.Key);
                    var newN = new Node(node.Key, node.Handle, table[nHash]);
                    table[nHash] = newN;
                }
            }

            for (var node = table[getIndex(key)]; node != null; node = node.Next) if (node.Key.Equals(key))
            {
                if (node.Handle.Target is IDisposable dispose) dispose.Dispose();
                node.Handle.Free();
                node.Handle = GCHandle.Alloc(value);
                return;
            }

            var hash = getIndex(key);
            var newNode = new Node(key, GCHandle.Alloc(value), table[hash]);
            table[hash] = newNode;
            ++count;
        }

        ///<inheritdoc/>
        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        ///<inheritdoc/>
        ///<exception cref="InvalidCastException"> The type of 'key' and/or 'value' cannot be casted to the respective value type. </exception>
        public void Add(object key, object value) => Add((TKey)key, (TValue)value);

        ///<inheritdoc/>
        public bool Remove(TKey key)
        {
            for (var node = table[getIndex(key)]; node != null; node = node.Next) if (node.Key.Equals(key))
            {
                node.Key = default;
                if (node.Handle.Target is IDisposable dispose) dispose.Dispose();
                node.Handle.Target = default;
                node.Handle.Free();

                return true;
            }

            return false;
        }

        ///<inheritdoc/>
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (item.Value.Equals(null)) throw new ArgumentNullException(nameof(item));
            for (var node = table[getIndex(item.Key)]; node != null; node = node.Next) if (node.Key.Equals(item.Key))
            {
                if (!node.Handle.Target.Equals(item.Value)) return false;

                node.Key = default;
                if (node.Handle.Target is IDisposable dispose) dispose.Dispose();
                node.Handle.Target = default;
                node.Handle.Free();

                return true;
            }

            return false;
        }

        ///<inheritdoc/>
        ///<exception cref="InvalidCastException"> The type of 'key' cannot be casted to the respective value type. </exception>
        public void Remove(object key) => Remove((TValue)key);

        ///<inheritdoc cref="IDictionary.Clear"/>
        public void Clear() => Dispose();

        ///<inheritdoc/>
        IDictionaryEnumerator IDictionary.GetEnumerator() => new Enumerator(this);

        readonly struct Enumerator : IDictionaryEnumerator
        {
            readonly IEnumerator<KeyValuePair<TKey, TValue>> enumerator;
            internal Enumerator(DisposableNativeDictionary<TKey, TValue> dictionary) => enumerator = dictionary.GetEnumerator();

            public DictionaryEntry Entry => new(enumerator.Current.Key, enumerator.Current.Value);
            public object Key => enumerator.Current.Key;
            public object Value => enumerator.Current.Value;
            public object Current => Entry;

            public bool MoveNext() => enumerator.MoveNext();
            public void Reset() => enumerator.Reset();
        }
    }
}