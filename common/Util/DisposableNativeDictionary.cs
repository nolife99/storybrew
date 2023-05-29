using System;
using System.Collections.Generic;

namespace StorybrewCommon.Util
{
    ///<summary> Represents a native collection of keys and <see cref="IDisposable"/> items. </summary>
    ///<typeparam name="T"> The type of the <see cref="IDisposable"/> object. </typeparam>
    public sealed class DisposableNativeDictionary<T> where T : class, IDisposable
    {
        class Node
        {
            internal string Key;
            internal T Value;
            internal Node Next;
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
                var index = getIndex(key);
                var current = table[index];
                while (current != null)
                {
                    if (current.Key == key) return current.Value;
                    current = current.Next;
                }
                throw new KeyNotFoundException($"The specified key was not found in the dictionary: {key}");
            }
            set
            {
                if (count / (float)table.Length >= .9f) resize();

                var index = getIndex(key);
                var current = table[index];
                while (current != null)
                {
                    if (current.Key == key)
                    {
                        current.Value = value;
                        return;
                    }
                    current = current.Next;
                }

                var newNode = new Node { Key = key, Value = value, Next = table[index] };
                table[index] = newNode;
                ++count;
            }
        }

        ///<summary> Attempts to get the <see cref="IDisposable"/> item associated with the specified key. </summary>
        ///<returns> A value indicating if <paramref name="key"/> was matched. If not, <paramref name="value"/> is returned <see langword="null"/>. </returns>
        public bool TryGetValue(string key, out T value)
        {
            var index = getIndex(key);
            var current = table[index];
            while (current != null)
            {
                if (current.Key == key)
                {
                    value = current.Value;
                    return true;
                }
                current = current.Next;
            }

            value = null;
            return false;
        }

        ///<summary> Releases all resources used by this <see cref="DisposableNativeDictionary{T}"/>, including the <see cref="IDisposable"/> items contained. </summary>
        public void Dispose()
        {
            for (var i = 0; i < table.Length; ++i)
            {
                var current = table[i];
                while (current != null)
                {
                    current.Value.Dispose();
                    current = current.Next;
                }
            }
        }

        void resize()
        {
            var oldTable = table;
            table = new Node[oldTable.Length * 2];

            for (var i = 0; i < count; ++i)
            {
                var current = oldTable[i];
                while (current != null)
                {
                    this[current.Key] = current.Value;
                    current = current.Next;
                }
            }
            count = 0;
        }
        int getIndex(string key) => (key.GetHashCode() & 0x7FFFFFFF) % table.Length;
    }
}