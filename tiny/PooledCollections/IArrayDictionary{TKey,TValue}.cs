namespace Tiny.PooledCollections;

using System.Collections.Generic;

public interface IArrayDictionary<TKey, TValue>
    : IDictionary<TKey, TValue>, IReadOnlyArrayDictionary<TKey, TValue>, ICollection<ArrayKeyValuePair<TKey, TValue>>
{
    void EnsureCapacity(int capacity);

    void IncreaseCapacityBy(int capacity);

    bool Remove(TKey key, out int index, out TValue value);

    void Set(TKey key, TValue value);

    bool TryAdd(TKey key, TValue value, out int index);
}