namespace StorybrewEditor.Util;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tiny.PooledCollections.Generic;

public sealed class AsyncActionQueue<T> : IDisposable
{
    readonly PooledList<ActionRunner> actionRunners;
    readonly bool allowDuplicates;
    readonly ActionQueueContext context;

    public AsyncActionQueue(bool allowDuplicates = false, int runnerCount = 0)
    {
        this.allowDuplicates = allowDuplicates;

        if (runnerCount == 0) runnerCount = int.Max(1, Environment.ProcessorCount - 1);
        context = new();

        actionRunners = new(runnerCount);
        for (var i = 0; i < runnerCount; ++i) actionRunners.Add(null);
    }

    public bool Enabled { get => context.Enabled; set => context.Enabled = value; }

    public bool Running => !context.Queue.IsEmpty || !context.Running.IsEmpty;
    public int TaskCount => context.Queue.Count + context.Running.Count;

    public void Queue(T target,
        int uniqueKey,
        Func<T, CancellationTokenSource, ValueTask> action,
        bool mustRunAlone = false)
    {
        for (var i = 0; i < int.Min(1 + (mustRunAlone ? 0 : TaskCount), actionRunners.Count); ++i)
            (actionRunners[i] ??= new(context)).EnsureThreadAlive();

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
            Parallel.ForEachAsync(actionRunners.Where(r => r is not null), (runner, _) => runner.DisposeAsync()) :
            Task.CompletedTask;
    }

    readonly record struct ActionContainer(T Target,
        int UniqueKey,
        Func<T, CancellationTokenSource, ValueTask> Action,
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

        public void Signal()
            => Interlocked.Exchange(ref tcs, new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();

        public Task WaitForSignal() => tcs.Task;
    }

    sealed class ActionRunner(ActionQueueContext context) : IAsyncDisposable
    {
        readonly ActionQueueContext context = context;
        Task thread;
        CancellationTokenSource tokenSrc;

        public async ValueTask DisposeAsync()
        {
            var localThread = Interlocked.Exchange(ref thread, null);
            if (localThread is null) return;

            context.Signal();

            if (await localThread.WaitAsync(TimeSpan.FromMilliseconds(400)).ContinueWith(t => t.IsFaulted))
                await tokenSrc.CancelAsync();

            await localThread;

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
                    var localToken = runner.tokenSrc;
                    var localContext = runner.context;

                    var mustSleep = false;
                    while (!localToken.IsCancellationRequested)
                    {
                        if (mustSleep)
                        {
                            try
                            {
                                await Task.Delay(200, localToken.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                return;
                            }

                            mustSleep = false;
                        }

                        while (!localContext.Enabled || localContext.Queue.IsEmpty)
                        {
                            if (Volatile.Read(ref runner.thread) is null) return;

                            await localContext.WaitForSignal();
                        }

                        if (Interlocked.CompareExchange(ref localContext.RunningLoneTask, false, false))
                        {
                            mustSleep = true;
                            continue;
                        }

                        ActionContainer task = default;
                        while (localContext.Queue.TryDequeue(out var t))
                        {
                            if (localContext.Running.ContainsKey(t.UniqueKey) ||
                                t.MustRunAlone && !localContext.Running.IsEmpty)
                            {
                                localContext.Queue.Enqueue(t);
                                continue;
                            }

                            task = t;
                            break;
                        }

                        if (task == default)
                        {
                            mustSleep = true;
                            continue;
                        }

                        localContext.Running.TryAdd(task.UniqueKey, true);
                        if (task.MustRunAlone) Interlocked.Exchange(ref localContext.RunningLoneTask, true);

                        await task.Action(task.Target, localToken);

                        if (task.MustRunAlone) Interlocked.Exchange(ref localContext.RunningLoneTask, false);
                        localContext.Running.TryRemove(task.UniqueKey, out _);
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