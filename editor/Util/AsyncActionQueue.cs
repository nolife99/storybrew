namespace StorybrewEditor.Util;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
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

    public int TaskCount
    {
        get
        {
            lock (context.Queue)
            lock (context.Running)
                return context.Queue.Count + context.Running.Count;
        }
    }

    public event Action<T, Exception> OnActionFailed
    {
        add => context.OnActionFailed += value;
        remove => context.OnActionFailed -= value;
    }

    public void Queue(T target, int uniqueKey, Action action, bool mustRunAlone = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        foreach (var runner in actionRunners) runner?.EnsureThreadAlive();
        lock (context.Queue)
        {
            if (!allowDuplicates && context.Queue.Exists(q => q.UniqueKey == uniqueKey)) return;
            context.Queue.Add(new(target, uniqueKey, action, mustRunAlone));
            Monitor.PulseAll(context.Queue);
        }
    }

    public Task CancelQueuedActions(bool stopThreads)
    {
        lock (context.Queue) context.Queue.Clear();
        return stopThreads ?
            Task.WhenAll(actionRunners.Where(runner => runner is not null).Select(runner => runner.DisposeAsync().AsTask())) :
            Task.CompletedTask;
    }

    sealed record ActionContainer(T Target, int UniqueKey, Action Action, bool MustRunAlone);

    sealed class ActionQueueContext(int runnerCount)
    {
        public readonly List<ActionContainer> Queue = new(runnerCount);
        public readonly HashSet<int> Running = new(runnerCount);

        bool enabled;
        public bool RunningLoneTask;

        public bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value) return;
                enabled = value;

                if (!enabled || Queue.Count == 0) return;
                lock (Queue) Monitor.PulseAll(Queue);
            }
        }

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

            lock (context.Queue) Monitor.PulseAll(context.Queue);

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

            thread = Task.Run(() =>
            {
                var mustSleep = false;
                while (!tokenSrc.IsCancellationRequested)
                {
                    if (mustSleep)
                    {
                        Thread.Yield();
                        mustSleep = false;
                    }

                    ActionContainer task = null;
                    lock (context.Queue)
                    {
                        while (!context.Enabled || context.Queue.Count == 0)
                        {
                            if (thread is null)
                            {
                                Trace.WriteLine($"Exiting thread {threadId}");
                                return;
                            }

                            Monitor.Wait(context.Queue);
                        }

                        lock (context.Running)
                        {
                            if (context.RunningLoneTask)
                            {
                                mustSleep = true;
                                continue;
                            }

                            var index = -1;
                            var queueSpan = CollectionsMarshal.AsSpan(context.Queue);

                            for (var i = 0; i < queueSpan.Length; ++i)
                            {
                                task = queueSpan[i];
                                if ((context.Running.Contains(task.UniqueKey) || task.MustRunAlone) &&
                                    (!task.MustRunAlone || context.Running.Count != 0)) continue;

                                index = i;
                                break;
                            }

                            if (index == -1)
                            {
                                mustSleep = true;
                                continue;
                            }

                            context.Queue.RemoveAt(index);
                            context.Running.Add(task!.UniqueKey);
                            if (task.MustRunAlone) context.RunningLoneTask = true;
                        }
                    }

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

                    lock (context.Running)
                    {
                        context.Running.Remove(task.UniqueKey);
                        if (task.MustRunAlone) context.RunningLoneTask = false;
                    }
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