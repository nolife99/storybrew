using System;
using System.Collections.Generic;

namespace StorybrewCommon.Util
{
    ///<summary> Represents a native collection of keys and <see cref="IDisposable"/> items. </summary>
    ///<typeparam name="T"> The type of the <see cref="IDisposable"/> object. </typeparam>
    public sealed class DisposableNativeDictionary<T> : IDisposable where T : IDisposable
    {
        class Node
        {
            internal string Key;
            internal T Value;
            internal Node Next;

            internal Node(string key, T value, Node next)
            {
                Key = key;
                Value = value;
                Next = next;
            }
        }

        static Node[] table;
        static int count;

        ///<summary> Creates a new <see cref="DisposableNativeDictionary{T}"/> object, with the given allocation capacity. </summary>
        public DisposableNativeDictionary(int capacity = 60) => table = new Node[capacity];

        ///<summary> Gets or sets the <see cref="IDisposable"/> item associated with the given key. </summary>
        ///<exception cref="KeyNotFoundException"> The specified key was not found in the dictionary. </exception>
        public T this[string key]
        {
            get
            {
                for (var node = table[getIndex(key)]; node != null; node = node.Next) if (node.Key == key)
                    return node.Value;

                throw new KeyNotFoundException($"The specified key was not found in the dictionary: {key}");
            }
            set
            {
                if (count / (float)table.Length >= .9f) resize();

                var index = getIndex(key);
                for (var node = table[index]; node != null; node = node.Next) if (node.Key == key)
                {
                    node.Value = value;
                    return;
                }

                var newNode = new Node(key, value, table[index]);
                table[index] = newNode;
                ++count;
            }
        }

        ///<summary> Attempts to get the <see cref="IDisposable"/> item associated with the specified key. </summary>
        ///<returns> A value indicating if <paramref name="key"/> was matched. If not, <paramref name="value"/> is returned <see langword="null"/>. </returns>
        public bool TryGetValue(string key, out T value)
        {
            for (var node = table[getIndex(key)]; node != null; node = node.Next) if (node.Key == key)
            {
                value = node.Value;
                return true;
            }

            value = default;
            return false;
        }

        ///<summary> Releases all resources used by this <see cref="DisposableNativeDictionary{T}"/>, including the <see cref="IDisposable"/> items contained. </summary>
        public void Dispose()
        {
            if (count == 0) return;

            for (var i = 0; i < table.Length; ++i) for (var node = table[i]; node != null; node = node.Next) 
                node.Value.Dispose();

            Array.Clear(table, 0, table.Length);
            count = 0;
        }

        void resize()
        {
            var oldTable = table;
            table = new Node[oldTable.Length * 2];

            for (var i = 0; i < count; ++i) for (var node = oldTable[i]; node != null; node = node.Next)
                this[node.Key] = node.Value;

            count = 0;
        }
        int getIndex(string key) => (key.GetHashCode() & 0x7FFFFFFF) % table.Length;
    }
}