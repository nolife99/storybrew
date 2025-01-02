namespace StorybrewEditor.Util;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class ThrottledActionScheduler
{
    readonly Lock _lock = new();
    readonly HashSet<string> scheduled = [];
    public int Delay = 100;

    public void Schedule(string key, Action<string> action) => Schedule(key,
        k =>
        {
            action(k);
            return true;
        });

    public void Schedule(string key, Func<string, bool> action)
    {
        lock (_lock)
            if (!scheduled.Add(key))
                return;

        Task.Delay(Delay)
            .ContinueWith(_ => Program.Schedule(() =>
            {
                lock (_lock) scheduled.Remove(key);
                if (!action(key)) Schedule(key, action);
            }));
    }
}