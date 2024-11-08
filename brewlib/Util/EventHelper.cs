namespace BrewLib.Util;

using System;

public static class EventHelper
{
    public static void InvokeStrict(Func<MulticastDelegate> getEventDelegate, Action<Delegate> raise)
    {
        var invocationList = getEventDelegate()?.GetInvocationList();
        if (invocationList is null) return;

        var first = true;
        foreach (var t in invocationList)
        {
            if (first) first = false;
            else
            {
                var currentList = getEventDelegate()?.GetInvocationList();
                if (currentList is null) return;

                if (!Array.Exists(currentList, h => h == t)) continue;
            }
            raise(t);
        }
    }
}