namespace BrewLib.Graphics.Textures;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using BrewLib.IO;
using BrewLib.Util;
using OpenTK.Windowing.Desktop;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tiny.PooledCollections.Generic;

public sealed class TextureContainerAsync : TextureContainer
{
    readonly ResourceContainer resourceContainer;
    readonly TextureOptions textureOptions;
    readonly PooledDictionary<string, TextureUploadQueue.QueuedUpload> textures;
    readonly PooledDictionary<string, TextureUploadQueue.QueuedUpload>.AlternateLookup<ReadOnlySpan<char>> texturesLookup;

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
                if (texture.IsLoaded)
                {
                    var size = texture.Result.Size;
                    pixels += size.X * size.Y;
                }

            return pixels / 1024 / 1024 * 4;
        }
    }

    public Texture2dRegion Get(scoped ReadOnlySpan<char> filename)
    {
        var found = texturesLookup.TryGetValue(filename, out var texture);
        switch (found)
        {
            case true when texture.IsLoaded: return texture.Result;

            case false:
                var str = filename.ToString();
                textures[str] = TextureUploadQueue.Queue(str, resourceContainer, textureOptions);
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

        foreach (var texture in textures.Values)
            if (texture.IsLoaded)
                texture.Result.Dispose();

        textures.Dispose();
        disposed = true;
    }

    #endregion
}

static class TextureUploadQueue
{
    const int UPLOAD_THREAD_COUNT = 2;
    static readonly ConcurrentQueue<QueuedUpload> queuedUploads = [];

    static readonly PooledList<Thread> threads = new();
    static readonly PooledList<NativeWindow> contexts = new();

    public static void Initialize()
    {
        Native.Window.Context.MakeNoneCurrent();

        for (var i = 0; i < UPLOAD_THREAD_COUNT; ++i)
        {
            NativeWindow window = new(new()
            {
                Title = "storybrew texture loader",
                Flags = Native.Window.Flags,
                StartVisible = false,
                SharedContext = Native.Window.Context,
                IsEventDriven = true,
                DepthBits = 0,
                StencilBits = 0,
                AlphaBits = 0
            });

            contexts.Add(window);

            window.Context.MakeNoneCurrent();

            Thread thread = new(context =>
            {
                ((IGLFWGraphicsContext)context)!.MakeCurrent();

                Trace.WriteLine("Started texture upload thread");

                while (!Native.Window.IsExiting)
                {
                    if (!queuedUploads.TryDequeue(out var queued))
                    {
                        Thread.Yield();
                        continue;
                    }

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
                        queuedUploads.Enqueue(queued);

                        // Happens when another process is writing to the file, will try again later.
                        continue;
                    }

                    if (stream is null)
                    {
                        Trace.TraceWarning($"Texture not found: {filename}");
                        continue;
                    }

                    using (var bitmap = Image.Load<Rgba32>(stream))
                    {
                        stream.Dispose();
                        queued.Result = Texture2d.Load(bitmap, queued.Options);
                    }

                    queued.IsLoaded = true;
                }
            }) { IsBackground = true };

            threads.Add(thread);

            thread.UnsafeStart(window.Context);
        }

        Native.Window.Context.MakeCurrent();
    }

    public static void Cleanup()
    {
        queuedUploads.Clear();

        foreach (var thread in threads) thread.Join();
        foreach (var context in contexts) context.Dispose();

        threads.Dispose();
        contexts.Dispose();
    }

    public static QueuedUpload Queue(string filename, ResourceContainer container, TextureOptions options)
    {
        QueuedUpload toQueue = new(filename, container, options);
        queuedUploads.Enqueue(toQueue);
        return toQueue;
    }

    public record QueuedUpload(string FileName, ResourceContainer Container, TextureOptions Options)
    {
        public bool IsLoaded;
        public Texture2d Result;
    }
}