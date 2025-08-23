namespace BrewLib.Graphics.Textures;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using BrewLib.IO;
using BrewLib.Util;
using OpenTK.Windowing.Desktop;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tiny.PooledCollections.Generic;
using Image = SixLabors.ImageSharp.Image;
using Monitor = System.Threading.Monitor;

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
    readonly PooledList<NativeWindow> contexts = new();

    readonly object enqueueSignal = new();
    readonly PooledQueue<QueuedUpload> queuedUploads = [];

    readonly PooledList<Thread> threads = new();

    public TextureUploadQueue()
    {
        var exiting = false;
        Native.Window.Closing += _ =>
        {
            exiting = true;
            Dispose();
        };

        Native.Window.Context.MakeNoneCurrent();

        for (var i = 0; i < UPLOAD_THREAD_COUNT; ++i)
        {
            NativeWindow window = new(new()
            {
                Title = "storybrew texture loader",
                Flags = Native.Window.Flags,
                StartVisible = false,
                StartFocused = false,
                SharedContext = Native.Window.Context,
                AutoLoadBindings = false,
                ClientSize = new(1),
                DepthBits = 0,
                StencilBits = 0,
                RedBits = 0,
                GreenBits = 0,
                BlueBits = 0,
                AlphaBits = 0
            });

            contexts.Add(window);

            window.Context.MakeNoneCurrent();

            Thread thread = new(context =>
            {
                ((IGLFWGraphicsContext)context)!.MakeCurrent();

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

                    var filename = queued.FileName;

                    Stream stream;
                    try
                    {
                        stream = File.Exists(filename) ?
                            File.OpenRead(filename) :
                            queued.Container?.GetStream(filename, ResourceSource.Embedded);
                    }
                    catch (IOException)
                    {
                        lock (queuedUploads) queuedUploads.Enqueue(queued);

                        // Happens when another process is writing to the file, will try again later.
                        continue;
                    }

                    if (stream is null)
                    {
                        Trace.TraceWarning($"Texture not found: {filename}");
                        continue;
                    }

                    using var bitmap = Image.Load<Rgba32>(stream);
                    stream.Dispose();
                    queued.Result = Texture2d.Load(bitmap, queued.Options);
                }
            });

            threads.Add(thread);

            thread.UnsafeStart(window.Context);

            Trace.WriteLine($"Started texture upload thread {i + 1}");
        }

        Native.Window.Context.MakeCurrent();
    }

    public void Dispose()
    {
        Clear();
        Signal();

        foreach (var thread in threads) thread.Join();
        foreach (var context in contexts) context.Dispose();

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