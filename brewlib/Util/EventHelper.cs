using System;

namespace BrewLib.Util;

public static class EventHelper
{
    public static void InvokeStrict(Func<MulticastDelegate> getEventDelegate, Action<Delegate> raise)
    {
        var invocationList = getEventDelegate()?.GetInvocationList();
        if (invocationList is null) return;

        var first = true;
        for (var i = 0; i < invocationList.Length; i++)
        {
            if (first) first = false;
            else
            {
                var currentList = getEventDelegate()?.GetInvocationList();
                if (currentList is null) return;

                if (!Array.Exists(currentList, h => h == invocationList[i])) continue;
            }
            raise(invocationList[i]);
        }
    }
}