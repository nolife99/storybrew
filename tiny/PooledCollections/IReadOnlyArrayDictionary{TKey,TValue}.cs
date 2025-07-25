namespace Tiny.PooledCollections;

using System;
using System.Collections.Generic;

public interface IReadOnlyArrayDictionary<TKey, TValue>
    : IReadOnlyDictionary<TKey, TValue>, IReadOnlyCollection<ArrayKeyValuePair<TKey, TValue>>
{
    bool ContainsValue(TValue value);

    int GetIndex(TKey key);

    bool TryFindIndex(TKey key, out int findIndex);

    void CopyTo(KeyValuePair<TKey, TValue>[] dest);

    void CopyTo(KeyValuePair<TKey, TValue>[] dest, int destIndex, int count);

    void CopyTo(scoped Span<KeyValuePair<TKey, TValue>> dest);

    void CopyTo(scoped Span<KeyValuePair<TKey, TValue>> dest, int destIndex);

    void CopyTo(scoped Span<KeyValuePair<TKey, TValue>> dest, int destIndex, int count);
}