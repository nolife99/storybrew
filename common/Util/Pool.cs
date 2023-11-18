using System;
using System.Collections.Generic;

namespace StorybrewCommon.Util
{
#pragma warning disable CS1591
    public sealed class Pool<T>(Func<T> allocator, Action<T> disposer = null, int poolCapacity = 256)
    {
        readonly Func<T> allocator = allocator;
        readonly Action<T> disposer = disposer;
        readonly Queue<T> queue = new(poolCapacity);

        public readonly int PoolCapacity = poolCapacity;
        int virtualCapacity;
        public int OptimalCapacity { get; private set; }

        public void PreAllocate()
        {
            while (queue.Count < PoolCapacity) queue.Enqueue(allocator());
        }
        public T Retrieve()
        {
            --virtualCapacity;
            virtualCapacity = Math.Max(0, virtualCapacity);

            if (queue.Count == 0) return allocator();
            return queue.Dequeue();
        }
        public void Release(IEnumerable<T> objects)
        {
            foreach (var obj in objects) Release(obj);
        }
        public void Release(T obj)
        {
            ++virtualCapacity;
            OptimalCapacity = Math.Max(virtualCapacity, OptimalCapacity);

            if (queue.Count > PoolCapacity) return;
            disposer?.Invoke(obj);
            queue.Enqueue(obj);
        }
    }
}