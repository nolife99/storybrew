namespace BrewLib.Graphics;

using System;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Collections.Pooled;

public sealed class DrawContext : IDisposable
{
    readonly PooledList<IDisposable> disposables = new();
    FrozenDictionary<Type, object> frozenReferences;
    PooledDictionary<Type, object> references = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>() where T : class => Unsafe.As<T>(frozenReferences.GetValueRefOrNullRef(typeof(T)));

    public void Register<T>(T obj, bool dispose = false) where T : class
    {
        if (references is null) throw new InvalidOperationException("Can't register to frozen DrawContext");

        references[typeof(T)] = obj;
        if (dispose && obj is IDisposable disposable) disposables.Add(disposable);
    }

    public void Freeze()
    {
        frozenReferences = references.ToFrozenDictionary();

        references.Dispose();
        references = null;
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        foreach (var disposable in disposables) disposable.Dispose();
        disposables.Dispose();

        disposed = true;
    }

    #endregion
}