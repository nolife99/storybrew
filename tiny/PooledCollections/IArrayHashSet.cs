namespace Tiny.PooledCollections;

using System.Collections.Generic;

public interface IArrayHashSet<T> : ICollection<T>, IReadOnlyCollection<T>
{
    bool Add(T item, out int index);

    void EnsureCapacity(int capacity);

    void IncreaseCapacityBy(int capacity);

    bool Remove(T item, out int index);
}