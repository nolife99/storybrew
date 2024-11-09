namespace BrewLib.Graphics.Compression;

using System;
using System.Diagnostics;
using System.IO;
using Data;
using Util;

public abstract class ImageCompressor(string utilityPath = null) : IDisposable
{
    protected ResourceContainer container;

    protected bool disposed;
    protected Process process;

    protected string utilName;

    public string UtilityPath { get; protected set; } =
        utilityPath ?? Path.GetDirectoryName(typeof(ImageCompressor).Assembly.Location) + "/cache/scripts";

    public virtual string UtilityName
    {
        get => StringHelper.GetMd5(utilName + Environment.CurrentManagedThreadId);
        protected set => utilName = value;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void LosslessCompress(string path) => compress(new Argument(path), false);
    public void Compress(string path) => compress(new Argument(path), true);

    public void LosslessCompress(string path, LosslessInputSettings settings) => compress(new Argument(path, settings), false);

    public void Compress(string path, LossyInputSettings settings) => compress(new Argument(path, null, settings), true);

    protected abstract void compress(Argument arg, bool useLossy);
    protected abstract string appendArgs(string path, bool useLossy, LossyInputSettings lossy, LosslessInputSettings lossless);

    protected abstract void ensureTool();
    protected void ensureStop()
    {
        process?.Dispose();
        process = null;
    }
    protected virtual string GetUtility() => Path.Combine(UtilityPath, UtilityName) + ".exe";

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            ensureStop();
            if (disposing)
            {
                container = null;
                disposed = true;
            }
        }
    }

    protected readonly struct Argument
    {
        internal readonly string path;
        internal readonly LosslessInputSettings lossless;
        internal readonly LossyInputSettings lossy;

        internal Argument(string path, LosslessInputSettings lossless = null, LossyInputSettings lossy = null)
        {
            this.path = path;
            this.lossless = lossless;
            this.lossy = lossy;
        }
    }
}