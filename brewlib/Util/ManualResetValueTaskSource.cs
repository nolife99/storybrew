namespace BrewLib.Util;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

public static class ValueTaskSourcePool
{
    static readonly ConcurrentQueue<ManualResetValueTaskSourceCore<byte>> queue = new();
    static ManualResetValueTaskSourceCore<byte> fastItem;

    public static ManualResetValueTaskSourceCore<byte> Get()
    {
        var item = fastItem;
        if (item is not null && Interlocked.CompareExchange(ref fastItem, null, item) == item || queue.TryDequeue(out item))
        {
            item.Reset();
            return item;
        }

        return new() { RunContinuationsAsynchronously = true };
    }

    public static void Return(ManualResetValueTaskSourceCore<byte> obj)
    {
        if (fastItem is not null || Interlocked.CompareExchange(ref fastItem, obj, null) is not null) queue.Enqueue(obj);
    }
}

public class ManualResetValueTaskSourceCore<TResult> : IValueTaskSource<TResult>, IValueTaskSource
{
    /// <summary>
    ///     A "captured" <see cref="SynchronizationContext"/> or <see cref="TaskScheduler"/> with which to invoke the callback,
    ///     or null if no special context is required.
    /// </summary>
    object _capturedContext;

    /// <summary>Whether the current operation has completed.</summary>
    bool _completed;

    /// <summary>
    ///     The callback to invoke when the operation completes if <see cref="OnCompleted"/> was called before the operation
    ///     completed, or <see cref="ManualResetValueTaskSourceCoreShared.s_sentinel"/> if the operation completed before a callback
    ///     was supplied, or null if a callback hasn't yet been provided and the operation hasn't yet completed.
    /// </summary>
    Action<object> _continuation;

    /// <summary>State to pass to <see cref="_continuation"/>.</summary>
    object _continuationState;

    /// <summary>The exception with which the operation failed, or null if it hasn't yet completed or completed successfully.</summary>
    ExceptionDispatchInfo _error;

    /// <summary><see cref="ExecutionContext"/> to flow to the callback, or null if no flowing is required.</summary>
    ExecutionContext _executionContext;

    /// <summary>The result with which the operation succeeded, or the default value if it hasn't yet completed or failed.</summary>
    TResult _result;

    /// <summary>Gets or sets whether to force continuations to run asynchronously.</summary>
    /// <remarks>Continuations may run asynchronously if this is false, but they'll never run synchronously if this is true.</remarks>
    public bool RunContinuationsAsynchronously { get; set; }

    /// <summary>Gets the operation version.</summary>
    public short Version { get; private set; }

    /// <inheritdoc/>
    void IValueTaskSource.GetResult(short token)
    {
        if (token != Version || !_completed || _error is not null) ThrowForFailedGetResult(token);
    }

    /// <summary>Gets the status of the operation.</summary>
    /// <param name="token">Opaque value that was provided to the <see cref="ValueTask"/>'s constructor.</param>
    public ValueTaskSourceStatus GetStatus(short token)
    {
        ValidateToken(token);
        return Volatile.Read(ref _continuation) == null || !_completed ? ValueTaskSourceStatus.Pending :
            _error == null ? ValueTaskSourceStatus.Succeeded :
            _error.SourceException is OperationCanceledException ? ValueTaskSourceStatus.Canceled :
            ValueTaskSourceStatus.Faulted;
    }

    /// <summary>Gets the result of the operation.</summary>
    /// <param name="token">Opaque value that was provided to the <see cref="ValueTask"/>'s constructor.</param>
    [StackTraceHidden] public TResult GetResult(short token)
    {
        if (token != Version || !_completed || _error is not null) ThrowForFailedGetResult(token);

        return _result!;
    }

    /// <summary>Schedules the continuation action for this operation.</summary>
    /// <param name="continuation">The continuation to invoke when the operation has completed.</param>
    /// <param name="state">The state object to pass to <paramref name="continuation"/> when it's invoked.</param>
    /// <param name="token">Opaque value that was provided to the <see cref="ValueTask"/>'s constructor.</param>
    /// <param name="flags">The flags describing the behavior of the continuation.</param>
    public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        ValidateToken(token);

        if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
            _executionContext = ExecutionContext.Capture();

        if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
        {
            var sc = SynchronizationContext.Current;
            if (sc is not null && sc.GetType() != typeof(SynchronizationContext)) _capturedContext = sc;
            else
            {
                var ts = TaskScheduler.Current;
                if (ts != TaskScheduler.Default) _capturedContext = ts;
            }
        }

        // We need to set the continuation state before we swap in the delegate, so that
        // if there's a race between this and SetResult/Exception and SetResult/Exception
        // sees the _continuation as non-null, it'll be able to invoke it with the state
        // stored here.  However, this also means that if this is used incorrectly (e.g.
        // awaited twice concurrently), _continuationState might get erroneously overwritten.
        // To minimize the chances of that, we check preemptively whether _continuation
        // is already set to something other than the completion sentinel.

        object oldContinuation = _continuation;
        if (oldContinuation is null)
        {
            _continuationState = state;
            oldContinuation = Interlocked.CompareExchange(ref _continuation, continuation, null);
        }

        if (oldContinuation is null) return;

        if (!ReferenceEquals(oldContinuation, ManualResetValueTaskSourceCoreShared.s_sentinel))
            throw new InvalidOperationException("The operation has already completed.");

        switch (_capturedContext)
        {
            case null:
                if (_executionContext != null) ThreadPool.QueueUserWorkItem(continuation, state, true);
                else ThreadPool.UnsafeQueueUserWorkItem(continuation, state, true);

                break;

            case SynchronizationContext sc:
                sc.Post(static s =>
                    {
                        var tuple = (Tuple<Action<object>, object>)s!;
                        tuple.Item1(tuple.Item2);
                    },
                    new Tuple<Action<object>, object>(continuation, state)); break;

            case TaskScheduler ts:
                Task.Factory.StartNew(continuation,
                    state,
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    ts); break;
        }
    }

    /// <summary>Resets to prepare for the next operation.</summary>
    public void Reset()
    {
        ++Version;
        _completed = false;
        _result = default;
        _error = null;
        _executionContext = null;
        _capturedContext = null;
        _continuation = null;
        _continuationState = null;
    }

    /// <summary>Completes with a successful result.</summary>
    /// <param name="result">The result.</param>
    public void SetResult(TResult result)
    {
        _result = result;
        SignalCompletion();
    }

    /// <summary>Completes with an error.</summary>
    /// <param name="error">The exception.</param>
    public void SetException(Exception error)
    {
        _error = ExceptionDispatchInfo.Capture(error);
        SignalCompletion();
    }

    [StackTraceHidden] void ThrowForFailedGetResult(short token)
    {
        if (token != Version || !_completed) throw new InvalidOperationException("The operation has not yet completed.");

        _error?.Throw();
        Debug.Fail($"{nameof(ThrowForFailedGetResult)} should never get here");
    }

    void ValidateToken(short token)
    {
        if (token != Version) throw new InvalidOperationException("The operation has already completed.");
    }

    void SignalCompletion()
    {
        if (_completed) return;

        _completed = true;

        if (Volatile.Read(ref _continuation) is null &&
            Interlocked.CompareExchange(ref _continuation, ManualResetValueTaskSourceCoreShared.s_sentinel, null) is null)
            return;

        Debug.Assert(_continuation is not null);

        if (_executionContext is null)
        {
            if (_capturedContext is null)
            {
                if (RunContinuationsAsynchronously)
                    ThreadPool.UnsafeQueueUserWorkItem(_continuation!, _continuationState, true);
                else _continuation!(_continuationState);
            }
            else InvokeSchedulerContinuation();
        }
        else InvokeContinuationWithContext();
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "CaptureForRestore")]
    static extern ExecutionContext GetCaptureForRestore(ExecutionContext c);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "RestoreInternal")]
    static extern void RestoreInternal(ExecutionContext c, ExecutionContext executionContext);

    void InvokeContinuationWithContext()
    {
        // This is in a helper as the error handling causes the generated asm
        // for the surrounding code to become less efficient (stack spills etc)
        // and it is an uncommon path.

        Debug.Assert(_continuation != null, $"Null {nameof(_continuation)}");
        Debug.Assert(_executionContext != null, $"Null {nameof(_executionContext)}");

        var currentContext = GetCaptureForRestore(null);

        // Restore the captured ExecutionContext before executing anything.
        ExecutionContext.Restore(_executionContext);

        if (_capturedContext is null)
        {
            if (RunContinuationsAsynchronously)
                try
                {
                    ThreadPool.QueueUserWorkItem(_continuation, _continuationState, true);
                }
                finally
                {
                    // Restore the current ExecutionContext.
                    RestoreInternal(null, currentContext);
                }
            else
            {
                // Running inline may throw; capture the edi if it does as we changed the ExecutionContext,
                // so need to restore it back before propagating the throw.
                ExceptionDispatchInfo edi = null;
                var syncContext = SynchronizationContext.Current;
                try
                {
                    _continuation(_continuationState);
                }
                catch (Exception ex)
                {
                    // Note: we have a "catch" rather than a "finally" because we want
                    // to stop the first pass of EH here.  That way we can restore the previous
                    // context before any of our callers' EH filters run.
                    edi = ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    // Set sync context back to what it was prior to coming in
                    SynchronizationContext.SetSynchronizationContext(syncContext);

                    // Restore the current ExecutionContext.
                    RestoreInternal(null, currentContext);
                }

                // Now rethrow the exception; if there is one.
                edi?.Throw();
            }

            return;
        }

        try
        {
            InvokeSchedulerContinuation();
        }
        finally
        {
            // Restore the current ExecutionContext.
            RestoreInternal(null, currentContext);
        }
    }

    /// <summary>
    ///     Invokes the continuation with the appropriate scheduler. This assumes that if <see cref="_continuation"/> is not
    ///     null we're already running within that <see cref="ExecutionContext"/>.
    /// </summary>
    void InvokeSchedulerContinuation()
    {
        Debug.Assert(_capturedContext != null, $"Null {nameof(_capturedContext)}");
        Debug.Assert(_continuation != null, $"Null {nameof(_continuation)}");

        switch (_capturedContext)
        {
            case SynchronizationContext sc:
                sc.Post(static s =>
                    {
                        var state = (Tuple<Action<object>, object>)s!;
                        state.Item1(state.Item2);
                    },
                    new Tuple<Action<object>, object>(_continuation, _continuationState)); break;

            case TaskScheduler ts:
                Task.Factory.StartNew(_continuation,
                    _continuationState,
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    ts); break;
        }
    }
}

internal static class ManualResetValueTaskSourceCoreShared // separated out of generic to avoid unnecessary duplication
{
    internal static readonly Action<object> s_sentinel = CompletionSentinel;

    static void CompletionSentinel(object _) // named method to aid debugging
    {
        Debug.Fail("The sentinel delegate should never be invoked.");
        throw new InvalidOperationException("The sentinel delegate should never be invoked.");
    }
}