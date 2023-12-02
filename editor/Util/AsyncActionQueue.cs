﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace StorybrewEditor.Util;

public sealed class AsyncActionQueue<T> : IDisposable
{
    readonly ActionQueueContext context;
    readonly List<ActionRunner> actionRunners;
    readonly bool allowDuplicates;

    public event Action<T, Exception> OnActionFailed
    {
        add => context.OnActionFailed += value;
        remove => context.OnActionFailed -= value;
    }

    public bool Enabled
    {
        get => context.Enabled;
        set => context.Enabled = value;
    }

    public int TaskCount
    {
        get
        {
            lock (context.Queue) lock (context.Running) return context.Queue.Count + context.Running.Count;
        }
    }

    public AsyncActionQueue(string threadName, bool allowDuplicates = false, int runnerCount = 0)
    {
        this.allowDuplicates = allowDuplicates;
        context = new();

        if (runnerCount == 0) runnerCount = Environment.ProcessorCount - 1;
        runnerCount = Math.Max(1, runnerCount);

        actionRunners = [];
        for (var i = 0; i < runnerCount; i++) actionRunners.Add(new(context, $"{threadName} #{i + 1}"));
    }

    public void Queue(T target, Action<T> action, bool mustRunAlone = false) => Queue(target, null, action, mustRunAlone);
    public void Queue(T target, string uniqueKey, Action<T> action, bool mustRunAlone = false)
    {
        ObjectDisposedException.ThrowIf(disposed, GetType());

        Parallel.ForEach(actionRunners, runner => runner.EnsureThreadAlive());
        lock (context.Queue)
        {
            if (!allowDuplicates && context.Queue.Any(q => q.Target.Equals(target))) return;
            context.Queue.Add(new(target, uniqueKey, action, mustRunAlone));
            Monitor.PulseAll(context.Queue);
        }
    }

    public void CancelQueuedActions(bool stopThreads)
    {
        lock (context.Queue) context.Queue.Clear();

        if (stopThreads)
        {
            var sw = Stopwatch.StartNew();
            Parallel.ForEach(actionRunners, runner => runner.JoinOrAbort(Math.Max(1000, 5000 - sw.ElapsedMilliseconds)));
            sw.Stop();
        }
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (!disposed)
        {
            context.Enabled = false;
            CancelQueuedActions(true);
            actionRunners.Clear();

            disposed = true;
        }
    }

    #endregion

    class ActionContainer(T target, string key, Action<T> action, bool runAlone)
    {
        internal T Target = target;
        internal string UniqueKey = key;
        internal Action<T> Action = action;
        internal bool MustRunAlone = runAlone;
    }

    class ActionQueueContext
    {
        internal readonly List<ActionContainer> Queue = [];
        internal readonly HashSet<string> Running = [];
        internal bool RunningLoneTask;

        internal event Action<T, Exception> OnActionFailed;
        internal bool TriggerActionFailed(T target, Exception e)
        {
            if (OnActionFailed is null) return false;

            OnActionFailed.Invoke(target, e);
            return true;
        }

        bool enabled;
        internal bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value) return;
                enabled = value;

                if (enabled) lock (Queue) if (Queue.Count > 0) Monitor.PulseAll(Queue);
            }
        }
    }

    class ActionRunner(ActionQueueContext context, string threadName)
    {
        CancellationTokenSource tokenSrc;
        Thread thread;

        internal void EnsureThreadAlive()
        {
            if (tokenSrc is null || tokenSrc.IsCancellationRequested)
            {
                tokenSrc?.Dispose();
                tokenSrc = new();
                tokenSrc.Token.Register(() => Trace.WriteLine($"Aborting thread {threadName}"));

                Thread localThread = null;

#pragma warning disable SYSLIB0046
                thread = localThread = new(() => ControlledExecution.Run(async () =>
                {
                    var mustSleep = false;
                    while (!tokenSrc.IsCancellationRequested)
                    {
                        if (mustSleep)
                        {
                            await Task.Delay(200);
                            mustSleep = false;
                        }

                        ActionContainer task;
                        lock (context.Queue)
                        {
                            while (!context.Enabled || context.Queue.Count == 0)
                            {
                                if (thread != localThread)
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

                                task = context.Queue.FirstOrDefault(t => !context.Running.Contains(t.UniqueKey) && !t.MustRunAlone || t.MustRunAlone && context.Running.Count == 0);
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
                        catch (ThreadAbortException)
                        {
                            Trace.WriteLine($"Aborted thread {threadName}");
                        }
                        catch (Exception e)
                        {
                            var target = task.Target;
                            if (!context.TriggerActionFailed(task.Target, e)) Trace.TraceError($"'{task.UniqueKey}' - Action failed: {e.GetType()} ({e})");
                        }

                        lock (context.Running)
                        {
                            context.Running.Remove(task.UniqueKey);
                            if (task.MustRunAlone) context.RunningLoneTask = false;
                        }
                    }
                }, tokenSrc.Token))
                {
                    Name = threadName,
                    IsBackground = true
                };
#pragma warning restore SYSLIB0046

                Trace.WriteLine($"Starting thread {threadName}");
                thread.Start();
            }
        }
        internal void JoinOrAbort(double millisecondsTimeout)
        {
            if (thread is null) return;

            var localThread = thread;
            thread = null;

            lock (context.Queue) Monitor.PulseAll(context.Queue);

            if (!localThread.Join((int)millisecondsTimeout))
            {
                tokenSrc.Cancel();
                localThread.Interrupt();
            }
            tokenSrc.Dispose();
        }
    }
}