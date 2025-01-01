namespace StorybrewEditor.Util;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

public sealed class AsyncActionQueue<T> : IDisposable
{
    readonly ActionRunner[] actionRunners;
    readonly bool allowDuplicates;
    readonly ActionQueueContext context;

    public AsyncActionQueue(bool allowDuplicates = false, int runnerCount = 0)
    {
        this.allowDuplicates = allowDuplicates;

        if (runnerCount == 0) runnerCount = Math.Max(1, Environment.ProcessorCount - 1);
        context = new(runnerCount);

        actionRunners = new ActionRunner[runnerCount];
        for (var i = 0; i < runnerCount; ++i) actionRunners[i] = new(context);
    }

    public bool Enabled { get => context.Enabled; set => context.Enabled = value; }

    public int TaskCount => context.Queue.Count + context.Running.Count;

    public event Action<T, Exception> OnActionFailed
    {
        add => context.OnActionFailed += value;
        remove => context.OnActionFailed -= value;
    }

    public void Queue(T target, int uniqueKey, Action action, bool mustRunAlone = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        foreach (var runner in actionRunners) runner?.EnsureThreadAlive();

        if (!allowDuplicates && context.Queue.Any(q => q.UniqueKey == uniqueKey)) return;

        context.Queue.Enqueue(new(target, uniqueKey, action, mustRunAlone));
        context.Signal();
    }

    public Task CancelQueuedActions(bool stopThreads)
    {
        context.Queue.Clear();
        return stopThreads ?
            Task.WhenAll(
                actionRunners.Where(runner => runner is not null).Select(runner => runner.DisposeAsync().AsTask())) :
            Task.CompletedTask;
    }

    sealed record ActionContainer(T Target, int UniqueKey, Action Action, bool MustRunAlone);

    sealed class ActionQueueContext(int runnerCount)
    {
        public readonly ConcurrentQueue<ActionContainer> Queue = [];
        public readonly ConcurrentDictionary<int, object> Running = new(runnerCount, runnerCount);
        bool enabled;
        public volatile bool RunningLoneTask;

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
        int threadId;
        CancellationTokenSource tokenSrc;

        public async ValueTask DisposeAsync()
        {
            if (thread is null) return;

            var localThread = thread;
            thread = null;

            context.Signal();

            if (await localThread.WaitAsync(TimeSpan.FromMilliseconds(400)).ContinueWith(t => t.IsFaulted))
            {
                await tokenSrc.CancelAsync();
                await localThread;
            }

            localThread.Dispose();
            tokenSrc.Dispose();
        }

        internal void EnsureThreadAlive()
        {
            if (tokenSrc is not null && !tokenSrc.IsCancellationRequested) return;

            tokenSrc?.Dispose();
            tokenSrc = new();

            thread = Task.Run(
                async () =>
                {
                    var mustSleep = false;
                    while (!tokenSrc.IsCancellationRequested)
                    {
                        if (mustSleep)
                        {
                            await Task.Delay(200);
                            mustSleep = false;
                        }

                        while (!context.Enabled || context.Queue.IsEmpty)
                        {
                            if (thread is null)
                            {
                                Trace.WriteLine($"Exiting thread {threadId}");
                                return;
                            }

                            await context.WaitForSignal();
                        }

                        if (context.RunningLoneTask)
                        {
                            mustSleep = true;
                            continue;
                        }

                        ActionContainer task = null;
                        while (context.Queue.TryDequeue(out var t))
                        {
                            if (t.MustRunAlone && !context.Running.IsEmpty)
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

                        context.Running.TryAdd(task.UniqueKey, null);
                        if (task.MustRunAlone) context.RunningLoneTask = true;

                        try
                        {
#pragma warning disable SYSLIB0046
                            ControlledExecution.Run(task.Action, tokenSrc.Token);
#pragma warning restore SYSLIB0046
                        }
                        catch (Exception e)
                        {
                            if (!tokenSrc.IsCancellationRequested) context.TriggerActionFailed(task.Target, e);
                        }

                        context.Running.Remove(task.UniqueKey, out _);
                        if (task.MustRunAlone) context.RunningLoneTask = false;
                    }
                });

            threadId = thread.Id;
            Trace.WriteLine($"Started thread {threadId}");
        }
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        context.Enabled = false;
        CancelQueuedActions(true).Wait();

        disposed = true;
    }

    #endregion
}