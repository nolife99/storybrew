namespace Tiny.PooledCollections;

public struct Entry<T>
{
    public int HashCode, Next;
    public T Value;
}