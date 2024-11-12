namespace BrewLib.Util;

using System;

public sealed class ActionDisposable(Action action) : IDisposable
{
    public void Dispose() => action();
}