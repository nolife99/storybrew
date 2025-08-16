namespace BrewLib.Util;

using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

public class ValueTaskSource<T>(bool runContinuationsAsynchronously) : IValueTaskSource<T>, IValueTaskSource
{
    ManualResetValueTaskSourceCore<T> _core = new() { RunContinuationsAsynchronously = runContinuationsAsynchronously };

    public ValueTask<T> Task => new(this, _core.Version);
    public ValueTask VoidTask => new(this, _core.Version);

    void IValueTaskSource.GetResult(short token) => _core.GetResult(token);

    public T GetResult(short token) => _core.GetResult(token);

    public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

    public void OnCompleted(Action<object> continuation,
        object state,
        short token,
        ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);

    public void SetResult(T result) => _core.SetResult(result);

    public void SetException(Exception error) => _core.SetException(error);

    public void Reset() => _core.Reset();
}