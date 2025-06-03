namespace Tiny.PooledCollections;

public struct Entry<TKey, TValue>
{
    public uint HashCode;
    public int Next;

    public TKey Key;
    public TValue Value;
}