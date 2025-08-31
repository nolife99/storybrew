namespace BrewLib.Graphics.Textures;

using System;
using System.IO;
using System.Threading;
using BrewLib.IO;
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
        foreach (var texture in textures.Values) texture.Result?.Dispose();

        textures.Dispose();
        disposed = true;
    }

    #endregion
}

sealed class TextureUploadQueue : IDisposable
{
    const int UPLOAD_THREAD_COUNT = 2;
    readonly PooledList<nint> contexts = new();

    readonly object enqueueSignal = new();
    readonly PooledQueue<QueuedUpload> queuedUploads = [];

    readonly PooledList<Thread> threads = new();
    readonly SDL.EventFilter watch;

    public TextureUploadQueue()
    {
        var exiting = false;

        watch = (nint _, ref SDL.Event e) =>
        {
            if (e.Type != (nint)SDL.EventType.Quit) return false;

            exiting = true;
            Dispose();
            return true;
        };

        SDL.AddEventWatch(watch, 0);

        var mainContext = SDL.GLGetCurrentContext();
        if (mainContext == 0) throw new InvalidOperationException($"Unable to get current context: {SDL.GetError()}");

        var currentWindow = SDL.GLGetCurrentWindow();
        if (currentWindow == 0) throw new InvalidOperationException($"Unable to get current window: {SDL.GetError()}");

        for (var i = 0; i < UPLOAD_THREAD_COUNT; ++i)
        {
            if (!SDL.GLSetAttribute(SDL.GLAttr.ShareWithCurrentContext, 1))
                throw new NotSupportedException($"Unable to share context: {SDL.GetError()}");

            var ctx = SDL.GLCreateContext(currentWindow);
            if (ctx == 0) throw new InvalidOperationException($"Unable to create context: {SDL.GetError()}");

            contexts.Add(ctx);

            if (!SDL.GLMakeCurrent(currentWindow, mainContext))
                throw new InvalidOperationException($"Unable to unbind context: {SDL.GetError()}");

            Thread thread = new(context =>
            {
                if (!SDL.GLMakeCurrent(currentWindow, (nint)context!))
                    throw new InvalidOperationException(
                        $"Unable to make context current on new thread: {SDL.GetError()}");

                while (!exiting)
                {
                    Monitor.Enter(queuedUploads);

                    if (!queuedUploads.TryDequeue(out var queued))
                    {
                        Monitor.Exit(queuedUploads);
                        lock (enqueueSignal) Monitor.Wait(enqueueSignal);

                        continue;
                    }

                    Monitor.Exit(queuedUploads);

                    Image<Rgba32> bitmap;
                    try
                    {
                        bitmap = Texture2d.LoadBitmap(queued.FileName);
                    }
                    catch (IOException)
                    {
                        lock (queuedUploads) queuedUploads.Enqueue(queued);

                        // Happens when another process is writing to the file, will try again later.
                        continue;
                    }

                    if (bitmap is null) continue;

                    queued.Result = Texture2d.Load(bitmap, queued.Options);
                    bitmap.Dispose();
                }
            });

            threads.Add(thread);

            thread.UnsafeStart(ctx);

            SDL.LogInfo(SDL.LogCategory.Video, $"Started texture upload thread {i + 1}");
        }
    }

    public void Dispose()
    {
        SDL.RemoveEventWatch(watch, 0);

        Clear();
        Signal();

        foreach (var thread in threads) thread.Join();
        foreach (var context in contexts) SDL.GLDestroyContext(context);

        threads.Dispose();
        contexts.Dispose();
    }

    public void Clear()
    {
        lock (queuedUploads) queuedUploads.Clear();
        Signal();
    }

    void Signal()
    {
        lock (enqueueSignal) Monitor.PulseAll(enqueueSignal);
    }

    public QueuedUpload Enqueue(string filename, ResourceContainer container, TextureOptions options)
    {
        QueuedUpload toQueue = new(filename, container, options);
        lock (queuedUploads) queuedUploads.Enqueue(toQueue);

        Signal();

        return toQueue;
    }

    public record QueuedUpload(string FileName, ResourceContainer Container, TextureOptions Options)
    {
        public volatile Texture2d Result;
        public bool IsLoaded => Result?.Wait(false) ?? false;
    }
}