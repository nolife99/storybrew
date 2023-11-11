using System;
using System.Collections.Generic;

namespace BrewLib.Graphics
{
    public class DrawContext : IDisposable
    {
        Dictionary<Type, object> references = new();
        List<IDisposable> disposables = new();

        public T Get<T>() => (T)references[typeof(T)];

        public void Register<T>(T obj, bool dispose = false)
        {
            references.Add(typeof(T), obj);
            if (dispose && obj is IDisposable disposable) disposables.Add(disposable);
        }

        #region IDisposable Support

        bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing) foreach (var disposable in disposables) disposable.Dispose();
                references = null;
                disposables = null;
                disposedValue = true;
            }
        }
        public void Dispose() => Dispose(true);

        #endregion
    }
}