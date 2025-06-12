namespace StorybrewEditor.Util;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed class AsyncActionQueue<T> : IDisposable
{
    readonly Lazy<ActionRunner>[] actionRunners;
    readonly bool allowDuplicates;
    readonly ActionQueueContext context;

    public AsyncActionQueue(bool allowDuplicates = false, int runnerCount = 0)
    {
        this.allowDuplicates = allowDuplicates;

        if (runnerCount == 0) runnerCount = int.Max(1, Environment.ProcessorCount - 1);
        context = new();

        actionRunners = ArrayPool<Lazy<ActionRunner>>.Shared.Rent(runnerCount);
        for (var i = 0; i < runnerCount; ++i) actionRunners[i] = new(() => new(context));
    }

    public bool Enabled { get => context.Enabled; set => context.Enabled = value; }

    public int TaskCount => context.Queue.Count + Interlocked.CompareExchange(ref context.Running, 0, 0);

    public event Action<T, Exception> OnActionFailed
    {
        add => context.OnActionFailed += value;
        remove => context.OnActionFailed -= value;
    }

    public void Queue(T target, int uniqueKey, Action<CancellationTokenSource> action, bool mustRunAlone = false)
    {
        for (var i = 0; i < int.Min(1 + (mustRunAlone ? 0 : TaskCount), actionRunners.Length); ++i)
            actionRunners[i]?.Value.EnsureThreadAlive();

        if (!allowDuplicates && context.Queue.Any(q => q.UniqueKey == uniqueKey)) return;

        context.Queue.Enqueue(new(target, uniqueKey, action, mustRunAlone));
        context.Signal();
    }

    public Task CancelQueuedActions(bool stopThreads)
    {
        context.Queue.Clear();
        return stopThreads ?
            Task.WhenAll(actionRunners.Where(runner => runner is not null && runner.IsValueCreated)
                .Select(runner => runner.Value.DisposeAsync().AsTask())) :
            Task.CompletedTask;
    }

    sealed record ActionContainer(T Target, int UniqueKey, Action<CancellationTokenSource> Action, bool MustRunAlone);

    sealed class ActionQueueContext
    {
        public readonly ConcurrentQueue<ActionContainer> Queue = [];
        bool enabled;
        public int Running;
        public bool RunningLoneTask;

        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value) return;

                enabled = value;

                if (!enabled || Queue.IsEmpty) return;

                Signal();
            }
        }

        public void Signal() => Interlocked.Exchange(
                ref tcs,
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
            .TrySetResult();

        public Task WaitForSignal() => tcs.Task;

        public event Action<T, Exception> OnActionFailed;
        public void TriggerActionFailed(T target, Exception e) => OnActionFailed?.Invoke(target, e);
    }

    sealed class ActionRunner(ActionQueueContext context) : IAsyncDisposable
    {
        Task thread;
        CancellationTokenSource tokenSrc;

        public async ValueTask DisposeAsync()
        {
            if (thread is null) return;

            var localThread = thread;
            thread = null;

            context.Signal();

            if (await localThread.WaitAsync(TimeSpan.FromMilliseconds(400)).ContinueWith(t => t.IsFaulted))
                await tokenSrc.CancelAsync();

            tokenSrc.Dispose();
        }

        internal void EnsureThreadAlive()
        {
            if (tokenSrc is not null && !tokenSrc.IsCancellationRequested) return;

            tokenSrc?.Dispose();
            tokenSrc = new();

            thread = Task.Factory.StartNew(async cancellationToken =>
                {
                    var localToken = (CancellationTokenSource)cancellationToken;
                    Trace.WriteLine($"Started thread {Environment.CurrentManagedThreadId}");

                    await using var registration = localToken.Token.UnsafeRegister(_
                            => Trace.WriteLine($"Aborting thread {Environment.CurrentManagedThreadId}"),
                        null);

                    var mustSleep = false;
                    while (!localToken.IsCancellationRequested)
                    {
                        if (mustSleep)
                        {
                            await Task.Delay(200, localToken.Token);
                            mustSleep = false;
                        }

                        while (!context.Enabled || context.Queue.IsEmpty)
                        {
                            if (thread is null)
                            {
                                Trace.WriteLine($"Exiting thread {Environment.CurrentManagedThreadId}");
                                return;
                            }

                            await context.WaitForSignal();
                        }

                        if (Interlocked.CompareExchange(ref context.RunningLoneTask, false, false))
                        {
                            mustSleep = true;
                            continue;
                        }

                        ActionContainer task = null;
                        while (context.Queue.TryDequeue(out var t))
                        {
                            if (t.MustRunAlone && Interlocked.CompareExchange(ref context.Running, 0, 0) != 0)
                            {
                                context.Queue.Enqueue(t);
                                continue;
                            }

                            task = t;
                            break;
                        }

                        if (task is null)
                        {
                            mustSleep = true;
                            continue;
                        }

                        Interlocked.Increment(ref context.Running);
                        if (task.MustRunAlone) Interlocked.Exchange(ref context.RunningLoneTask, true);

                        try
                        {
                            task.Action(localToken);
                        }
                        catch (Exception e)
                        {
                            if (!localToken.IsCancellationRequested) context.TriggerActionFailed(task.Target, e);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref context.Running);
                            if (task.MustRunAlone) Interlocked.Exchange(ref context.RunningLoneTask, false);
                        }
                    }
                },
                tokenSrc,
                tokenSrc.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        context.Enabled = false;
        CancelQueuedActions(true).Wait();

        ArrayPool<Lazy<ActionRunner>>.Shared.Return(actionRunners);

        disposed = true;
    }

    #endregion
}