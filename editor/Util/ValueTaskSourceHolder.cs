namespace StorybrewEditor.Util;

using System;
using System.Collections.Concurrent;
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
            queuedAction.TaskSource.SetResult(true);
        }
        catch (Exception e)
        {
            queuedAction.TaskSource.SetException(e);
        }

        ValueTaskSourceHolderShared.TaskSourcePool.Enqueue(queuedAction.TaskSource);

        queuedAction = default;
        Pool.Release(this);
    }

    public static ValueTaskSourceHolder<TState> Get(Action<TState> action, TState state)
    {
        if (!ValueTaskSourceHolderShared.TaskSourcePool.TryDequeue(out var taskSource)) taskSource = new(true);
        else taskSource.Reset();

        var holder = Pool.Retrieve();
        holder.queuedAction = new(action, state, taskSource);
        return holder;
    }

    readonly record struct QueuedAction(Action<TState> Action, TState State, ValueTaskSource<bool> TaskSource);
}

static class ValueTaskSourceHolderShared
{
    public static readonly ConcurrentQueue<ValueTaskSource<bool>> TaskSourcePool = new();
}