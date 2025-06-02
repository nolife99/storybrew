namespace Tiny.PooledCollections;

public interface IPredicate<in T>
{
    bool Predicate(T value);
}