namespace BrewLib.Graphics;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public sealed class DrawContext : IDisposable
{
    readonly List<IDisposable> disposables = [];
    readonly Dictionary<Type, object> references = [];
    FrozenDictionary<Type, object> frozenReferences;

    public T Get<T>() where T : class => Unsafe.As<T>(frozenReferences.GetValueRefOrNullRef(typeof(T)));

    public void Register<T>(T obj, bool dispose = false) where T : class
    {
        references[typeof(T)] = obj;
        if (dispose && obj is IDisposable disposable) disposables.Add(disposable);
    }
    public void Freeze() => frozenReferences = references.ToFrozenDictionary();

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (disposed) return;
        foreach (var disposable in disposables) disposable.Dispose();
        disposed = true;
    }

    #endregion
}