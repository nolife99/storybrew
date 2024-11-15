namespace StorybrewEditor.Util;

using System;
using System.Collections.Generic;
using System.Threading;

public class ThrottledActionScheduler
{
    readonly HashSet<string> scheduled = [];
    readonly Lock _lock = new();
    public int Delay = 100;

    public void Schedule(string key, Action<string> action) => Schedule(key, k =>
    {
        action(k);
        return true;
    });

    public void Schedule(string key, Func<string, bool> action)
    {
        lock (_lock)
            if (!scheduled.Add(key))
                return;

        Program.Schedule(() =>
        {
            lock (_lock) scheduled.Remove(key);
            if (!action(key)) Schedule(key, action);
        }, Delay);
    }
}