using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StorybrewEditor.Util
{
    public class AsyncActionQueue<T> : IDisposable
    {
        readonly ActionQueueContext context = new();
        readonly List<ActionRunner> actionRunners = new();
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
            if (disposedValue) throw new ObjectDisposedException(nameof(AsyncActionQueue<T>));

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
        protected virtual void Dispose(bool disposing)
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
            public T Target;
            public string UniqueKey;
            public Action<T> Action;
            public bool MustRunAlone;
        }

        class ActionQueueContext
        {
            public readonly List<ActionContainer> Queue = new();
            public readonly HashSet<string> Running = new();
            public bool RunningLoneTask;

            public event ActionFailed OnActionFailed;
            public bool TriggerActionFailed(T target, Exception e)
            {
                if (OnActionFailed is null) return false;

                OnActionFailed.Invoke(target, e);
                return true;
            }

            bool enabled;
            public bool Enabled
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

            CancellationTokenSource cancellationTokenSource;
            CancellationToken cancellationToken;

            Task thread;

            public ActionRunner(ActionQueueContext context, string threadName)
            {
                this.context = context;
                this.threadName = threadName;
            }

            public void EnsureThreadAlive()
            {
                if (cancellationTokenSource is null || cancellationTokenSource.IsCancellationRequested)
                {
                    cancellationTokenSource?.Dispose();
                    cancellationTokenSource = new CancellationTokenSource();
                    cancellationToken = cancellationTokenSource.Token;

                    Task localThread = null;
                    thread = localThread = new Task(async () =>
                    {
                        var mustSleep = false;
                        while (true)
                        {
                            if (mustSleep)
                            {
                                await Task.Delay(200);
                                mustSleep = false;
                            }

                            ActionContainer task;
                            lock (context.Queue)
                            {
                                // Check if we want to start cancelling
                                while (!context.Enabled || context.Queue.Count == 0)
                                {
                                    // Cancel the thread if the instance's thread is set to null
                                    if (thread != localThread)
                                    {
                                        Trace.WriteLine($"Exiting thread {threadName}.");
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
                    }, cancellationToken);

                    Trace.WriteLine($"Starting thread {threadName}.");
                    thread.Start();
                }
            }

            public void JoinOrAbort(int millisecondsTimeout)
            {
                if (thread is null) return;

                var localThread = thread;

                // Set the instance's task to null to trigger an exit
                thread = null;

                lock (context.Queue) Monitor.PulseAll(context.Queue);

                // If an exit hasn't happened (for example, the thread is unresponsive) try to force-stop it
                if (!localThread.Wait(millisecondsTimeout))
                {
                    Trace.WriteLine($"Aborting thread {threadName}.");
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                }
                localThread.Dispose();
            }
        }
    }
}