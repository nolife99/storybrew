namespace BrewLib.Graphics;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public sealed class DrawContext : IDisposable
{
    readonly List<IDisposable> disposables = [];
    FrozenDictionary<int, object> frozenReferences;
    List<KeyValuePair<int, object>> references = [];

    public T Get<T>() where T : class => Unsafe.As<T>(frozenReferences.GetValueRefOrNullRef(TypeKeyCache<T>.Key));

    public void Register<T>(T obj, bool dispose = false) where T : class
    {
        if (references is null) throw new InvalidOperationException("Can't register to frozen DrawContext");

        references.Add(new(TypeKeyCache<T>.Key, obj));
        if (dispose && obj is IDisposable disposable) disposables.Add(disposable);
    }

    public void Freeze()
    {
        frozenReferences = references.ToFrozenDictionary();
        references = null;
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        for (var i = disposables.Count - 1; i >= 0; --i)
            disposables[i].Dispose();

        disposed = true;
    }

    #endregion
}

file static class TypeKeyCache<T>
{
    public static readonly int Key = typeof(T).MetadataToken;
}