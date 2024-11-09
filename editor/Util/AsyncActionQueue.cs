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
    readonly bool allowDuplicates;
    readonly ActionQueueContext context;
    List<ActionRunner> actionRunners;

    public AsyncActionQueue(string threadName, bool allowDuplicates = false, int runnerCount = 0)
    {
        this.allowDuplicates = allowDuplicates;
        context = new();

        if (runnerCount == 0) runnerCount = Environment.ProcessorCount - 1;
        runnerCount = Math.Max(1, runnerCount);

        actionRunners = [];
        Parallel.For(0, runnerCount, i => actionRunners.Add(new(context, $"{threadName} #{i + 1}")));
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

    public void Queue(T target, Action<T> action, bool mustRunAlone = false) => Queue(target, null, action, mustRunAlone);

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

    public void CancelQueuedActions(bool stopThreads)
    {
        lock (context.Queue) context.Queue.Clear();
        if (!stopThreads) return;

        var sw = Stopwatch.StartNew();
        foreach (var runner in actionRunners.Where(runner => runner is not null))
        {
            runner.msTimeout = Math.Max(1000, 5000 - sw.Elapsed.Milliseconds);
            runner.Dispose();
        }

        sw.Stop();
    }

    class ActionContainer(T target, string key, Action<T> action, bool runAlone)
    {
        internal readonly Action<T> Action = action;
        internal readonly bool MustRunAlone = runAlone;
        internal readonly string UniqueKey = key;
        internal T Target = target;
    }

    class ActionQueueContext
    {
        internal readonly List<ActionContainer> Queue = [];
        internal readonly HashSet<string> Running = [];

        bool enabled;
        internal bool RunningLoneTask;

        internal bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value) return;
                enabled = value;

                if (!enabled) return;
                lock (Queue)
                    if (Queue.Count > 0)
                        Monitor.PulseAll(Queue);
            }
        }

        internal event Action<T, Exception> OnActionFailed;

        internal void TriggerActionFailed(T target, Exception e)
        {
            if (OnActionFailed is null) return;
            OnActionFailed.Invoke(target, e);
        }
    }

    class ActionRunner(ActionQueueContext context, string threadName) : IDisposable
    {
        internal int msTimeout;
        Task thread;
        CancellationTokenSource tokenSrc;

        public void Dispose()
        {
            if (thread is null) return;

            var localThread = thread;
            thread = null;

            lock (context.Queue) Monitor.PulseAll(context.Queue);

            if (!localThread.Wait(msTimeout))
            {
                tokenSrc.Cancel();
                localThread.Dispose();
            }

            tokenSrc.Dispose();
        }

        internal void EnsureThreadAlive()
        {
            if (tokenSrc is not null && !tokenSrc.IsCancellationRequested) return;
            tokenSrc?.Dispose();
            tokenSrc = new();

#pragma warning disable SYSLIB0046
            thread = Task.Factory.StartNew(() => ControlledExecution.Run(() =>
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
                                Trace.WriteLine($"Exiting thread {threadName} gracefully");
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
                                => context.Running.Add(t.UniqueKey) && !t.MustRunAlone ||
                                t.MustRunAlone && context.Running.Count == 0);

                            if (task is null)
                            {
                                mustSleep = true;
                                continue;
                            }

                            context.Queue.Remove(task);
                            if (task.MustRunAlone) context.RunningLoneTask = true;
                        }
                    }

                    try
                    {
                        task.Action(task.Target);
                    }
                    catch (ThreadAbortException)
                    {
                        Trace.WriteLine($"Aborted thread {threadName}");
                    }
                    catch (Exception e)
                    {
                        var target = task.Target;
                        context.TriggerActionFailed(task.Target, e);
                    }

                    lock (context.Running)
                    {
                        context.Running.Remove(task.UniqueKey);
                        if (task.MustRunAlone) context.RunningLoneTask = false;
                    }
                }
            }, tokenSrc.Token), TaskCreationOptions.LongRunning);
#pragma warning restore SYSLIB0046

            Trace.WriteLine($"Starting thread {threadName} ({thread.Id})");
        }
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose() => Dispose(true);

    void Dispose(bool disposing)
    {
        if (disposed) return;
        context.Enabled = false;
        CancelQueuedActions(true);

        if (disposing)
        {
            actionRunners.Clear();
            actionRunners = null;
        }

        disposed = true;
    }

    #endregion
}