namespace StorybrewEditor.Util;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tiny.PooledCollections.Generic;

public sealed class AsyncActionQueue<T> : IDisposable
{
    readonly PooledList<Lazy<ActionRunner>> actionRunners;
    readonly bool allowDuplicates;
    readonly ActionQueueContext context;

    public AsyncActionQueue(bool allowDuplicates = false, int runnerCount = 0)
    {
        this.allowDuplicates = allowDuplicates;

        if (runnerCount == 0) runnerCount = int.Max(1, Environment.ProcessorCount - 1);
        context = new();

        actionRunners = new(runnerCount);
        for (var i = 0; i < runnerCount; ++i) actionRunners.Add(new(() => new(context)));
    }

    public bool Enabled { get => context.Enabled; set => context.Enabled = value; }

    public int TaskCount => context.Queue.Count + context.Running.Count;

    public event Action<T, Exception> OnActionFailed
    {
        add => context.OnActionFailed += value;
        remove => context.OnActionFailed -= value;
    }

    public void Queue(T target, int uniqueKey, Func<CancellationTokenSource, ValueTask> action, bool mustRunAlone = false)
    {
        for (var i = 0; i < int.Min(1 + (mustRunAlone ? 0 : TaskCount), actionRunners.Count); ++i)
            actionRunners[i].Value.EnsureThreadAlive();

        if (!allowDuplicates)
            foreach (var runner in context.Queue)
                if (runner.UniqueKey == uniqueKey)
                    return;

        context.Queue.Enqueue(new(target, uniqueKey, action, mustRunAlone));
        context.Signal();
    }

    public Task CancelQueuedActions(bool stopThreads)
    {
        context.Queue.Clear();
        return stopThreads ?
            Task.WhenAll(actionRunners.Where(runner => runner.IsValueCreated).Select(runner => runner.Value.JoinOrAbort())) :
            Task.CompletedTask;
    }

    sealed record ActionContainer(T Target,
        int UniqueKey,
        Func<CancellationTokenSource, ValueTask> Action,
        bool MustRunAlone);

    sealed class ActionQueueContext
    {
        public readonly ConcurrentQueue<ActionContainer> Queue = [];
        public readonly ConcurrentDictionary<int, bool> Running = [];
        bool enabled;
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

    sealed class ActionRunner(ActionQueueContext context)
    {
        readonly ActionQueueContext context = context;
        Task thread;
        CancellationTokenSource tokenSrc;

        public async Task JoinOrAbort()
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

            thread = Task.Factory.StartNew(async actionRunner =>
                {
                    var runner = (ActionRunner)actionRunner;
                    var threadId = runner.thread.Id;
                    var localToken = runner.tokenSrc;
                    var localContext = runner.context;

                    Trace.WriteLine($"Started thread {threadId}");

                    await using var registration = localToken.Token.UnsafeRegister(_
                            => Trace.WriteLine($"Aborting thread {threadId}"),
                        null);

                    var mustSleep = false;
                    while (!localToken.IsCancellationRequested)
                    {
                        if (mustSleep)
                        {
                            await Task.Delay(200, localToken.Token);
                            mustSleep = false;
                        }

                        while (!localContext.Enabled || localContext.Queue.IsEmpty)
                        {
                            if (runner.thread is null)
                            {
                                Trace.WriteLine($"Exiting thread {threadId}");
                                return;
                            }

                            await localContext.WaitForSignal();
                        }

                        if (Interlocked.CompareExchange(ref localContext.RunningLoneTask, false, false))
                        {
                            mustSleep = true;
                            continue;
                        }

                        ActionContainer task = null;
                        while (localContext.Queue.TryDequeue(out var t))
                        {
                            if (localContext.Running.ContainsKey(t.UniqueKey) ||
                                t.MustRunAlone && localContext.Running.IsEmpty)
                            {
                                localContext.Queue.Enqueue(t);
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

                        localContext.Running.TryAdd(task.UniqueKey, true);
                        if (task.MustRunAlone) Interlocked.Exchange(ref localContext.RunningLoneTask, true);

                        try
                        {
                            await task.Action(localToken);
                        }
                        catch (Exception e)
                        {
                            if (!localToken.IsCancellationRequested) localContext.TriggerActionFailed(task.Target, e);
                        }
                        finally
                        {
                            localContext.Running.TryRemove(task.UniqueKey, out _);
                            if (task.MustRunAlone) Interlocked.Exchange(ref localContext.RunningLoneTask, false);
                        }
                    }
                },
                this,
                tokenSrc.Token);
        }
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        context.Enabled = false;
        CancelQueuedActions(true).Wait();

        actionRunners.Dispose();

        disposed = true;
    }

    #endregion
}