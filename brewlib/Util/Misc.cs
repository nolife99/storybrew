namespace BrewLib.Util;

using System;
using System.Diagnostics;
using System.Threading;

public static class Misc
{
    public static void WithRetries(Action action, int timeout = 1500, bool canThrow = true)
    {
        var sleepTime = 0;
        while (true)
            try
            {
                action();
                return;
            }
            catch (Exception e)
            {
                if (sleepTime >= timeout)
                {
                    if (canThrow) throw;
                    Trace.TraceError($"Retryable action: {e}");
                    return;
                }

                var retryDelay = timeout / 10;
                Thread.Sleep(retryDelay);
                sleepTime += retryDelay;
            }
    }
    public static T WithRetries<T>(Func<T> action, int timeout = 1500, bool canThrow = true)
    {
        var sleepTime = 0;
        while (true)
            try
            {
                return action();
            }
            catch (Exception e)
            {
                if (sleepTime >= timeout)
                {
                    if (canThrow) throw;
                    Trace.TraceError($"Retryable action: {e}");
                    return default;
                }

                var retryDelay = timeout / 10;
                Thread.Sleep(retryDelay);
                sleepTime += retryDelay;
            }
    }
}