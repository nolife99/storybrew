﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StorybrewEditor.Util
{
    public sealed class AsyncActionQueue<T> : IDisposable
    {
        readonly ActionQueueContext context = new ActionQueueContext();
        readonly List<ActionRunner> actionRunners = new List<ActionRunner>();
        readonly bool allowDuplicates;

        readonly SemaphoreSlim semaphore = new SemaphoreSlim(Environment.ProcessorCount - 1);

        public delegate void ActionFailedEventHandler(T target, Exception e);
        public event ActionFailedEventHandler OnActionFailed
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

            if (runnerCount == 0) runnerCount = Environment.ProcessorCount - 1;
            runnerCount = Math.Max(1, runnerCount);

            for (var i = 0; i < runnerCount; ++i) actionRunners.Add(new ActionRunner(context, $"{threadName} #{i + 1}"));
        }

        public void Queue(T target, Action<T> action, bool mustRunAlone = false) => Queue(target, null, action, mustRunAlone);
        public void Queue(T target, string uniqueKey, Action<T> action, bool mustRunAlone = false)
        {
            if (disposed) throw new ObjectDisposedException(nameof(AsyncActionQueue<T>));
            foreach (var r in actionRunners) r.EnsureThreadAlive();

            lock (context.Queue)
            {
                if (!allowDuplicates) foreach (var q in context.Queue) if (q.Target.Equals(target)) return;

                context.Queue.Add(new ActionContainer
                {
                    Target = target,
                    UniqueKey = uniqueKey,
                    Action = action,
                    MustRunAlone = mustRunAlone
                });
                Monitor.PulseAll(context.Queue);
            }
        }
        public void CancelQueuedActions(bool stopThreads)
        {
            semaphore.Wait();
            try
            {
                context.Queue.Clear();
            }
            finally
            {
                semaphore.Release();
            }

            if (stopThreads)
            {
                var sw = Stopwatch.StartNew();
                foreach (var r in actionRunners) r.JoinOrAbort(Math.Max(1000, 2000 - (int)sw.ElapsedMilliseconds));
            }
        }

        #region IDisposable Support

        bool disposed;
        void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    context.Enabled = false;
                    CancelQueuedActions(true);
                }
                disposed = true;
            }
        }
        public void Dispose() => Dispose(true);

        #endregion

        struct ActionContainer
        {
            internal T Target;
            internal string UniqueKey;
            internal Action<T> Action;
            internal bool MustRunAlone;
        }
        class ActionQueueContext
        {
            internal readonly List<ActionContainer> Queue = new List<ActionContainer>();
            internal readonly HashSet<string> Running = new HashSet<string>();
            internal bool RunningLoneTask;

            internal event ActionFailedEventHandler OnActionFailed;
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
        class ActionRunner
        {
            readonly ActionQueueContext context;
            readonly string threadName;

            Thread thread;

            internal ActionRunner(ActionQueueContext context, string threadName)
            {
                this.context = context;
                this.threadName = threadName;
            }

            internal void EnsureThreadAlive()
            {
                if (thread == null || !thread.IsAlive)
                {
                    Thread localThread = null;
                    thread = localThread = new Thread(async () =>
                    {
                        var mustSleep = false;
                        for (;;)
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
                                        Trace.WriteLine($"Exiting thread {localThread.Name}.");
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

                                    task = context.Queue.FirstOrDefault(t => !context.Running.Contains(t.UniqueKey)
                                        && !t.MustRunAlone || t.MustRunAlone && context.Running.Count == 0);

                                    if (task.Action is null && task.UniqueKey is null)
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
                                var target = task.Target;
                                Program.Schedule(() =>
                                {
                                    if (!context.TriggerActionFailed(target, e)) Trace.WriteLine($"Action failed for '{task.UniqueKey}': {e}");
                                });
                            }

                            lock (context.Running)
                            {
                                context.Running.Remove(task.UniqueKey);
                                if (task.MustRunAlone) context.RunningLoneTask = false;
                            }
                        }
                    })
                    {
                        Name = threadName,
                        IsBackground = true
                    };

                    Trace.WriteLine($"Starting thread {thread.Name}.");
                    thread.Start();
                }
            }
            internal void JoinOrAbort(int millisecondsTimeout)
            {
                if (thread == null) return;

                var localThread = thread;
                thread = null;

                lock (context.Queue) Monitor.PulseAll(context.Queue);

                using (var cancellationTokenSource = new CancellationTokenSource(millisecondsTimeout))
                {
                    var completed = false;

                    if (!completed) Trace.WriteLine($"Canceling thread {localThread.Name}");
                    while (!completed && !cancellationTokenSource.Token.IsCancellationRequested) completed = localThread.Join(10);
                    if (completed) Trace.WriteLine($"Canceled thread {localThread.Name}");
                }
            }
        }
    }
}