namespace BrewLib.Util;

using System;
using System.Linq;

public static class EventHelper
{
    public static void InvokeStrict(MulticastDelegate eventDelegate, Action<Delegate> raise)
    {
        var invocationList = eventDelegate?.GetInvocationList();
        if (invocationList is null) return;

        var first = true;
        foreach (var t in invocationList)
        {
            if (first) first = false;
            else if (!eventDelegate.GetInvocationList().Contains(t)) continue;

            raise(t);
        }
    }
}