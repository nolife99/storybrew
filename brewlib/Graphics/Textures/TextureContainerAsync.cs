namespace BrewLib.Graphics.Textures;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using BrewLib.IO;
using BrewLib.Util;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tiny.PooledCollections.Generic;

public sealed class TextureContainerAsync : TextureContainer
{
    static readonly TextureUploadQueue uploadQueue = new();
    readonly ResourceContainer resourceContainer;
    readonly TextureOptions textureOptions;

    readonly PooledDictionary<string, TextureUploadQueue.QueuedUpload> textures;

    readonly PooledDictionary<string, TextureUploadQueue.QueuedUpload>.AlternateLookup<ReadOnlySpan<char>>
        texturesLookup;

    public TextureContainerAsync(ResourceContainer resourceContainer = null, TextureOptions textureOptions = null)
    {
        this.resourceContainer = resourceContainer;
        this.textureOptions = textureOptions;

        textures = new();
        texturesLookup = textures.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public float UncompressedMemoryUseMb
    {
        get
        {
            var pixels = 0f;
            foreach (var texture in textures.Values)
                if (texture.Result is not null)
                {
                    var size = texture.Result.Size;
                    pixels += size.X * size.Y;
                }

            return pixels / 1024 / 1024 * 4;
        }
    }

    public Texture2dRegion Get(scoped ReadOnlySpan<char> filename)
    {
        switch (texturesLookup.TryGetValue(filename, out var texture))
        {
            case true when texture.IsLoaded: return texture.Result;

            case false:
                var str = filename.ToString();
                textures[str] = uploadQueue.Enqueue(str, resourceContainer, textureOptions);
                break;
        }

        return DrawState.TransparentPixel;
    }

    public Texture2dRegion Add(Image<Rgba32> bitmap, TextureOptions options = null)
        => Texture2d.Load(bitmap, textureOptions);

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        uploadQueue.Clear();
        foreach (var texture in textures.Values) Interlocked.Exchange(ref texture.Result, null)?.Dispose();

        textures.Dispose();
        disposed = true;
    }

    #endregion
}

sealed class TextureUploadQueue : IDisposable
{
    const int UPLOAD_THREAD_COUNT = 2;

    readonly object enqueueSignal = new();
    readonly ConcurrentQueue<QueuedUpload> queuedUploads = [];

    readonly PooledList<Thread> threads = new();
    readonly EventFilter watch;

    public TextureUploadQueue()
    {
        var exiting = false;
        SDL.AddEventWatch(watch = (nint _, ref Event e) =>
            {
                if (e.Type is not EventType.Quit) return false;

                exiting = true;
                Native.MainThreadScheduler(target => ((IDisposable)target).Dispose(), this);

                return true;
            },
            0);

        var mainContext = SDL.GLGetCurrentContext();
        if (mainContext == 0) throw new InvalidOperationException($"Unable to get current context: {SDL.GetError()}");

        var currentWindow = SDL.GLGetCurrentWindow();
        if (currentWindow == 0) throw new InvalidOperationException($"Unable to get current window: {SDL.GetError()}");

        for (var i = 0; i < UPLOAD_THREAD_COUNT; ++i)
        {
            if (!SDL.GLSetAttribute(GLAttr.ShareWithCurrentContext, 1))
                throw new NotSupportedException($"Unable to share context: {SDL.GetError()}");

            var ctx = SDL.GLCreateContext(currentWindow);
            if (ctx == 0) throw new InvalidOperationException($"Unable to create shared context: {SDL.GetError()}");

            if (!SDL.GLMakeCurrent(currentWindow, mainContext))
                throw new InvalidOperationException($"Unable to unbind shared context: {SDL.GetError()}");

            Thread thread = new(context =>
            {
                while (!SDL.GLMakeCurrent(currentWindow, (nint)context!)) { }

                while (!exiting)
                {
                    if (!queuedUploads.TryDequeue(out var queued))
                    {
                        lock (enqueueSignal) Monitor.Wait(enqueueSignal);
                        continue;
                    }

                    Image<Rgba32> bitmap;
                    try
                    {
                        bitmap = Texture2d.LoadBitmap(queued.FileName);
                    }
                    catch (IOException)
                    {
                        queuedUploads.Enqueue(queued);

                        // Happens when another process is writing to the file, will try again later.
                        continue;
                    }

                    if (bitmap is null) continue;

                    queued.Result = Texture2d.Load(bitmap, queued.Options);
                    bitmap.Dispose();
                }

                SDL.GLDestroyContext((nint)context);
            });

            thread.UnsafeStart(ctx);
            threads.Add(thread);

            SDL.LogInfo(SDL.LogCategory.Video, $"Started texture upload thread {i + 1}");
        }
    }

    public void Dispose()
    {
        SDL.RemoveEventWatch(watch, 0);

        Clear();
        Signal();

        foreach (var thread in threads) thread.Join();

        threads.Dispose();
    }

    public void Clear()
    {
        queuedUploads.Clear();
        Signal();
    }

    void Signal()
    {
        lock (enqueueSignal) Monitor.PulseAll(enqueueSignal);
    }

    public QueuedUpload Enqueue(string filename, ResourceContainer container, TextureOptions options)
    {
        QueuedUpload toQueue = new(filename, container, options);
        queuedUploads.Enqueue(toQueue);

        Signal();

        return toQueue;
    }

    public record QueuedUpload(string FileName, ResourceContainer Container, TextureOptions Options)
    {
        public Texture2d Result;
        public bool IsLoaded => Result?.Wait(false) ?? false;
    }
}