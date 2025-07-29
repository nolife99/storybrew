namespace StorybrewEditor.Util;

using System;
using System.Threading.Tasks;
using BrewLib.Memory;
using BrewLib.Util;

sealed class ValueTaskSourceHolder<TState> : IDisposable
{
    static readonly Pool<ValueTaskSourceHolder<TState>> Pool = new();

    QueuedAction queuedAction;

    public ValueTask Task => queuedAction.TaskSource.VoidTask;

    public void Dispose()
    {
        try
        {
            queuedAction.Action(queuedAction.State);
            queuedAction.TaskSource.SetResult(0);
        }
        catch (Exception e)
        {
            queuedAction.TaskSource.SetException(e);
        }

        ValueTaskSourcePool<byte>.Return(queuedAction.TaskSource);

        queuedAction = default;
        Pool.Release(this);
    }

    public static ValueTaskSourceHolder<TState> Get(Action<TState> action, TState state)
    {
        var holder = Pool.Retrieve();
        holder.queuedAction = new(action, state, ValueTaskSourcePool<byte>.Get());
        return holder;
    }

    readonly record struct QueuedAction(Action<TState> Action, TState State, ValueTaskSource<byte> TaskSource);
}