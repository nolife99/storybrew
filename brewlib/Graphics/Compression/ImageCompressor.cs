﻿namespace BrewLib.Graphics.Compression;

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using IO;

public abstract class ImageCompressor(string utilityPath = null) : IDisposable
{
    protected ResourceContainer container;

    protected bool disposed;
    protected Process process;

    protected string utilName;

    public string UtilityPath { get; protected set; } = utilityPath ?? Path.GetTempPath();

    public string UtilityName
    {
        get => HashCode.Combine(utilName, Environment.CurrentManagedThreadId).ToString(CultureInfo.InvariantCulture);
        protected set => utilName = value;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~ImageCompressor() => Dispose(false);

    public void LosslessCompress(string path, LosslessInputSettings settings)
        => InternalCompress(new Argument(path, settings), false);

    public void Compress(string path, LossyInputSettings settings)
        => InternalCompress(new Argument(path, null, settings), true);

    protected abstract void InternalCompress(Argument arg, bool useLossy);

    protected abstract string appendArgs(string path,
        bool useLossy,
        LossyInputSettings lossy,
        LosslessInputSettings lossless);

    protected abstract void ensureTool();

    protected void ensureStop()
    {
        process?.Dispose();
        process = null;
    }

    protected string GetUtility() => Path.Combine(UtilityPath, UtilityName) + ".exe";

    protected virtual void Dispose(bool disposing)
    {
        if (disposed) return;

        ensureStop();
        if (disposing) disposed = true;
    }

    protected readonly record struct Argument(string path,
        LosslessInputSettings lossless = null,
        LossyInputSettings lossy = null);
}