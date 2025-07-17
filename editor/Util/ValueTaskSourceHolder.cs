namespace StorybrewEditor.Util;

using System;
using System.Threading.Tasks;
using BrewLib.Memory;
using BrewLib.Util;

internal sealed class ValueTaskSourceHolder<TState> : IDisposable
{
    static readonly Pool<ValueTaskSourceHolder<TState>> Pool = new();

    QueuedAction _queuedAction;

    public ValueTask Task => new(_queuedAction.TaskSource, _queuedAction.TaskSource.Version);

    public void Dispose()
    {
        try
        {
            _queuedAction.Action(_queuedAction.State);
            _queuedAction.TaskSource.SetResult(0);
        }
        catch (Exception e)
        {
            _queuedAction.TaskSource.SetException(e);
        }

        ValueTaskSourcePool<byte>.Return(_queuedAction.TaskSource);

        _queuedAction = default;
        Pool.Release(this);
    }

    public static ValueTaskSourceHolder<TState> Get(Action<TState> action, TState state)
    {
        var holder = Pool.Retrieve();
        holder._queuedAction = new(action, state, ValueTaskSourcePool<byte>.Get());
        return holder;
    }

    readonly record struct QueuedAction(Action<TState> Action,
        TState State,
        ManualResetValueTaskSourceCore<byte> TaskSource);
}