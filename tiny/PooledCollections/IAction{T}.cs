namespace Tiny.PooledCollections;

public interface IAction<in T>
{
    void Action(T value);
}