using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

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
            while (true)
            {
                try
                {
                    return action();
                }
                catch (Exception e)
                {
                    if (sleepTime >= timeout)
                    {
                        if (canThrow) throw;
                        else
                        {
                            Trace.Write($"Retryable action failed:{e}");
                            return default;
                        }
                    }

                    var retryDelay = timeout / 10;
                    sleepTime += retryDelay;
                    using (var wait = Task.Delay(retryDelay)) wait.Wait();
                }
            }
        }
    }
}