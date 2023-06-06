using System;
using System.Collections;
using System.Collections.Generic;

namespace StorybrewCommon.Util
{
    ///<summary> Represents a fast native collection of keys and items. </summary>
    ///<typeparam name="TKey"> The type of the keys in the dictionary. </typeparam>
    ///<typeparam name="TValue"> The type of the values in the dictionary. </typeparam>
    public sealed class DisposableNativeDictionary<TKey, TValue> : IDisposable, IEnumerable<KeyValuePair<TKey, TValue>>
    {
        class Node
        {
            internal TKey Key;
            internal TValue Value;
            internal Node Next;

            internal Node(TKey key, TValue value, Node next)
            {
                Key = key;
                Value = value;
                Next = next;
            }
        }

        Node[] table;
        int count;

        ///<summary> Creates a new <see cref="DisposableNativeDictionary{TKey, TValue}"/> object, with the given allocation capacity. </summary>
        public DisposableNativeDictionary(int capacity = 32) => table = new Node[capacity];

        ///<summary> Gets or sets the item associated with the given key. 
        ///<para/> If <paramref name="key"/> does not match an existing key in the collection, the value will be added. </summary>
        ///<exception cref="KeyNotFoundException"> The specified key was not found in the dictionary. </exception>
        public TValue this[TKey key]
        {
            get
            {
                for (var node = table[getIndex(key)]; node != null; node = node.Next) if (node.Key.Equals(key))
                    return node.Value;

                throw new KeyNotFoundException($"The specified key was not found in the dictionary: {key}");
            }
            set
            {
                if (count / (float)table.Length >= 1)
                {
                    var oldTable = table;
                    table = new Node[oldTable.Length * 2];

                    for (var i = 0; i < oldTable.Length; ++i) for (var node = oldTable[i]; node != null; node = node.Next)
                    {
                        var nIndex = getIndex(node.Key);
                        var newN = new Node(node.Key, node.Value, table[nIndex]);
                        table[nIndex] = newN;
                    }
                }

                for (var node = table[getIndex(key)]; node != null; node = node.Next) if (node.Key.Equals(key))
                {
                    node.Value = value;
                    return;
                }

                var index = getIndex(key);
                var newNode = new Node(key, value, table[index]);
                table[index] = newNode;
                ++count;
            }
        }

        ///<summary> Returns a collection of keys that are contained within this dictionary. </summary>
        public IEnumerable<TKey> Keys
        {
            get
            {
                for (var i = 0; i < table.Length; ++i) for (var node = table[i]; node != null; node = node.Next)
                    yield return node.Key;
            }
        }

        ///<summary> Returns a collection of values that are contained within this dictionary. </summary>
        public IEnumerable<TValue> Values
        {
            get
            {
                for (var i = 0; i < table.Length; ++i) for (var node = table[i]; node != null; node = node.Next)
                    yield return node.Value;
            }
        }

        ///<summary> Attempts to get the item associated with the specified key. </summary>
        ///<returns> A value indicating if <paramref name="key"/> was matched. If not, <paramref name="value"/> is returned <see langword="null"/>. </returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            for (var node = table[getIndex(key)]; node != null; node = node.Next) if (node.Key.Equals(key))
            {
                value = node.Value;
                return true;
            }

            value = default;
            return false;
        }

        ///<inheritdoc/>
        public void Dispose()
        {
            if (count == 0) return;

            for (var i = 0; i < table.Length; ++i) for (var node = table[i]; node != null; node = node.Next)
                if (node.Value is IDisposable value) value.Dispose();

            Array.Clear(table, 0, table.Length);
            count = 0;
        }

        int getIndex(TKey key) => (key.GetHashCode() & 0x7FFFFFFF) % table.Length;

        ///<inheritdoc/>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (var i = 0; i < table.Length; ++i) for (var node = table[i]; node != null; node = node.Next)
                yield return new KeyValuePair<TKey, TValue>(node.Key, node.Value);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}