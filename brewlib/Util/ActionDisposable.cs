using System;

namespace BrewLib.Util;

public sealed class ActionDisposable(Action action) : IDisposable
{
    public void Dispose() => action();
}