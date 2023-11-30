using System;

namespace BrewLib.Util;

public sealed class ActionDisposable(Action action) : IDisposable
{
    readonly Action action = action;

    public void Dispose() => action();
}