using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace StorybrewEditor.Util
{
    public sealed class AsyncActionQueue<T> : IDisposable
    {
        readonly ActionQueueContext context = new();
        readonly List<ActionRunner> actionRunners = [];
        readonly bool allowDuplicates;

        public delegate void ActionFailed(T target, Exception e);
        public event ActionFailed OnActionFailed
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

            for (var i = 0; i < runnerCount; i++) actionRunners.Add(new ActionRunner(context, $"{threadName} #{i + 1}"));
        }

        public void Queue(T target, Action<T> action, bool mustRunAlone = false) => Queue(target, null, action, mustRunAlone);
        public void Queue(T target, string uniqueKey, Action<T> action, bool mustRunAlone = false)
        {
            ObjectDisposedException.ThrowIf(disposedValue, typeof(AsyncActionQueue<T>));

            Parallel.For(0, actionRunners.Count, i => actionRunners[i].EnsureThreadAlive());
            lock (context.Queue)
            {
                if (!allowDuplicates && context.Queue.Any(q => q.Target.Equals(target))) return;

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
            lock (context.Queue) context.Queue.Clear();

            if (stopThreads)
            {
                var sw = Stopwatch.StartNew();
                Parallel.For(0, actionRunners.Count, i => actionRunners[i].JoinOrAbort(Math.Max(1000, 5000 - (int)sw.ElapsedMilliseconds)));
                sw.Stop();
            }
        }

        #region IDisposable Support

        bool disposedValue;
        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    context.Enabled = false;
                    CancelQueuedActions(true);
                }
                disposedValue = true;
            }
        }

        public void Dispose() => Dispose(true);

        #endregion

        class ActionContainer
        {
            internal T Target;
            internal string UniqueKey;
            internal Action<T> Action;
            internal bool MustRunAlone;
        }

        class ActionQueueContext
        {
            internal readonly List<ActionContainer> Queue = [];
            internal readonly HashSet<string> Running = [];
            internal bool RunningLoneTask;

            internal event ActionFailed OnActionFailed;
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
            readonly ActionQueueContext context = context;
            readonly string threadName = threadName;

            CancellationTokenSource tokenSrc;
            Task thread;

            internal void EnsureThreadAlive()
            {
                if (tokenSrc is null || tokenSrc.IsCancellationRequested)
                {
                    tokenSrc?.Dispose();
                    tokenSrc = new CancellationTokenSource();

                    Task localThread = null;

#pragma warning disable SYSLIB0046 // ControlledExecution is obsolete
                    thread = localThread = new Task(() => ControlledExecution.Run(async () =>
                    {
                        var mustSleep = false;
                        while (!tokenSrc.IsCancellationRequested)
                        {
                            if (mustSleep)
                            {
                                using (var wait = Task.Delay(200)) await wait;
                                mustSleep = false;
                            }

                            ActionContainer task;
                            lock (context.Queue)
                            {
                                // Check if we want to start cancelling
                                while (!context.Enabled || context.Queue.Count == 0)
                                {
                                    // Cancel the thread if the instance's thread is invalidated
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
                            catch (Exception e)
                            {
                                var target = task.Target;
                                if (!context.TriggerActionFailed(task.Target, e) && thread == localThread) Trace.WriteLine($"Action failed for '{task.UniqueKey}': {e.Message}");
                            }

                            lock (context.Running)
                            {
                                context.Running.Remove(task.UniqueKey);
                                if (task.MustRunAlone) context.RunningLoneTask = false;
                            }
                        }
                    }, tokenSrc.Token), tokenSrc.Token, TaskCreationOptions.LongRunning);
#pragma warning restore SYSLIB0046

                    Trace.WriteLine($"Starting thread {threadName}");
                    thread.Start();
                }
            }
            internal async void JoinOrAbort(int millisecondsTimeout)
            {
                if (thread is null) return;
                var localThread = thread;

                // Invalidate the instance's task to trigger an exit
                thread = null;

                lock (context.Queue) Monitor.PulseAll(context.Queue);

                // If an exit hasn't happened (e.g. the thread is unresponsive) try to force-stop it
                if (!localThread.Wait(millisecondsTimeout, tokenSrc.Token))
                {
                    Trace.WriteLine($"Aborting thread {threadName}");
                    tokenSrc.Cancel();

                    try
                    {
                        await localThread;
                    }
                    catch (AggregateException)
                    {
                        Trace.WriteLine($"Aborted thread {threadName}");
                    }
                }

                localThread.Dispose();
                tokenSrc.Dispose();
            }
        }
    }
}