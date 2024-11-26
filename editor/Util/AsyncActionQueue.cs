namespace StorybrewEditor.Util;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

public sealed class AsyncActionQueue<T> : IDisposable
{
    readonly List<ActionRunner> actionRunners;
    readonly bool allowDuplicates;
    readonly ActionQueueContext context;

    public AsyncActionQueue(string threadName, bool allowDuplicates = false, int runnerCount = 0)
    {
        this.allowDuplicates = allowDuplicates;
        context = new();

        if (runnerCount == 0) runnerCount = Math.Max(1, Environment.ProcessorCount - 1);

        actionRunners = [];
        for (var i = 0; i < runnerCount; ++i) actionRunners.Add(new(context, $"{threadName} #{i + 1}"));
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

    public void Queue(T target, string uniqueKey, Action<T> action, bool mustRunAlone = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        foreach (var runner in actionRunners) runner?.EnsureThreadAlive();
        lock (context.Queue)
        {
            if (!allowDuplicates && context.Queue.Exists(q => q.Target.Equals(target))) return;
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

    sealed record ActionContainer(T Target, string UniqueKey, Action<T> Action, bool MustRunAlone);

    sealed class ActionQueueContext
    {
        public readonly List<ActionContainer> Queue = [];
        public readonly HashSet<string> Running = [];

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

    sealed class ActionRunner(ActionQueueContext context, string threadName) : IAsyncDisposable
    {
        Task thread;
        CancellationTokenSource tokenSrc;

        public async ValueTask DisposeAsync()
        {
            if (thread is null) return;

            var localThread = thread;
            thread = null;

            lock (context.Queue) Monitor.PulseAll(context.Queue);

            if (!localThread.Wait(500))
            {
                await tokenSrc.CancelAsync().ConfigureAwait(false);
                try
                {
                    await localThread.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Trace.WriteLine($"Killed thread {threadName}");
                }
            }

            localThread.Dispose();
            tokenSrc.Dispose();
        }

        internal void EnsureThreadAlive()
        {
            if (tokenSrc is not null && !tokenSrc.IsCancellationRequested) return;
            tokenSrc?.Dispose();
            tokenSrc = new();

#pragma warning disable SYSLIB0046
            thread = Task.Run(() => ControlledExecution.Run(() =>
            {
                var mustSleep = false;
                while (!tokenSrc.IsCancellationRequested)
                {
                    if (mustSleep)
                    {
                        Thread.Sleep(200);
                        mustSleep = false;
                    }

                    ActionContainer task;
                    lock (context.Queue)
                    {
                        while (!context.Enabled || context.Queue.Count == 0)
                        {
                            if (thread is null)
                            {
                                Trace.WriteLine($"Exiting thread {threadName}");
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

                            task = context.Queue.Find(t
                                => !context.Running.Contains(t.UniqueKey) && !t.MustRunAlone ||
                                t.MustRunAlone && context.Running.Count == 0);

                            if (task is null)
                            {
                                mustSleep = true;
                                continue;
                            }

                            context.Queue.Remove(task);
                            context.Running.Add(task.UniqueKey);
                            if (task.MustRunAlone) context.RunningLoneTask = true;
                        }
                    }

                    try
                    {
                        task.Action(task.Target);
                    }
                    catch (Exception e)
                    {
                        if (e is not ThreadAbortException) context.TriggerActionFailed(task.Target, e);
                    }

                    lock (context.Running)
                    {
                        context.Running.Remove(task.UniqueKey);
                        if (task.MustRunAlone) context.RunningLoneTask = false;
                    }
                }
            }, tokenSrc.Token), tokenSrc.Token);
#pragma warning restore SYSLIB0046

            Trace.WriteLine($"Started thread {threadName} ({thread.Id})");
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