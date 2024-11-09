﻿namespace StorybrewEditor.Util;

using System;
using System.Collections.Generic;

/// <summary>
///     Schedules an action on the main thread until it succeeds. Actions come with a key that prevents queuing the
///     same one multiple times.
/// </summary>
public class ThrottledActionScheduler
{
    readonly HashSet<string> scheduled = [];
    public int Delay = 100;

    public void Schedule(string key, Action<string> action) => Schedule(key, k =>
    {
        action(k);
        return true;
    });

    public void Schedule(string key, Func<string, bool> action)
    {
        lock (scheduled)
            if (!scheduled.Add(key))
                return;

        Program.Schedule(() =>
        {
            lock (scheduled) scheduled.Remove(key);
            if (!action(key)) Schedule(key, action);
        }, Delay);
    }
}