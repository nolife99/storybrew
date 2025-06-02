namespace Tiny.PooledCollections;

using System.Collections.Generic;

public interface IArrayHashSet<T> : ICollection<T>, IReadOnlyCollection<T>
{
    bool Add(in T item);

    bool Add(T item, out int index);

    bool Add(in T item, out int index);

    bool Contains(in T item);

    void EnsureCapacity(int capacity);

    void IncreaseCapacityBy(int capacity);

    bool Remove(in T item);

    bool Remove(in T item, out int index);

    bool Remove(T item, out int index);
}