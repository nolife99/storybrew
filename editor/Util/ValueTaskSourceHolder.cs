namespace StorybrewEditor.Util;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BrewLib.Util;

sealed class ValueTaskSourceHolder<TState> : IDisposable
{
    static readonly Queue<ValueTaskSourceHolder<TState>> Pool = new();
    static readonly Lock PoolLock = new();

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

        lock (ValueTaskSourceHolderShared<bool>.PoolLock)
            ValueTaskSourceHolderShared<bool>.TaskSourcePool.Enqueue(queuedAction.TaskSource);

        queuedAction = default;
        lock (PoolLock) Pool.Enqueue(this);
    }

    public static ValueTaskSourceHolder<TState> Get(Action<TState> action, TState state)
    {
        ValueTaskSourceHolderShared<bool>.PoolLock.Enter();

        if (!ValueTaskSourceHolderShared<bool>.TaskSourcePool.TryDequeue(out var taskSource)) taskSource = new(true);
        else taskSource.Reset();

        ValueTaskSourceHolderShared<bool>.PoolLock.Exit();

        PoolLock.Enter();
        if (!Pool.TryDequeue(out var holder)) holder = new();
        PoolLock.Exit();

        holder.queuedAction = new(action, state, taskSource);
        return holder;
    }

    readonly record struct QueuedAction(Action<TState> Action, TState State, ValueTaskSource<bool> TaskSource);
}

static class ValueTaskSourceHolderShared<T>
{
    public static readonly Queue<ValueTaskSource<T>> TaskSourcePool = new([new(true)]);
    public static readonly Lock PoolLock = new();
}