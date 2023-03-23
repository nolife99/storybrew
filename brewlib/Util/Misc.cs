using System;
using System.Diagnostics;
using System.Threading;

namespace BrewLib.Util
{
    public static class Misc
    {
        public static void WithRetries(Action action, int timeout = 1500, bool canThrow = true) => WithRetries(() =>
        {
            action();
            return true;
        }, timeout, canThrow);
        public static T WithRetries<T>(Func<T> action, int timeout = 1500, bool canThrow = true)
        {
            var sleepTime = 0;
            var wait = new ManualResetEventSlim();

            while (true)
            {
                try
                {
                    wait.Dispose();
                    return action();
                }
                catch (Exception e)
                {
                    if (sleepTime >= timeout)
                    {
                        if (canThrow)
                        {
                            wait.Dispose();
                            throw;
                        }
                        else
                        {
                            Trace.Write($"Retryable action failed:{e}");
                            wait.Dispose();
                            return default;
                        }
                    }

                    var retryDelay = timeout / 10;
                    sleepTime += retryDelay;
                    wait.Wait(retryDelay);
                }
            }
        }
    }
}