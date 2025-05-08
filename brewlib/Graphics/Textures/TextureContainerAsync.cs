namespace BrewLib.Graphics.Textures;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Collections.Pooled;
using IO;
using OpenTK.Windowing.Desktop;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using Util;

public sealed class TextureContainerAsync(ResourceContainer resourceContainer = null,
    TextureOptions textureOptions = null) : TextureContainer
{
    readonly PooledDictionary<string, TextureUploadQueue.QueuedUpload> textures = new();

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

    public Texture2dRegion Get(string filename)
    {
        var found = textures.TryGetValue(filename, out var texture);
        switch (found)
        {
            case true when texture.IsLoaded: return texture.Result;
            case false: textures[filename] = TextureUploadQueue.Queue(filename, resourceContainer, textureOptions); break;
        }

        return DrawState.TransparentPixel;
    }

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

internal static class TextureUploadQueue
{
    const int UPLOAD_THREAD_COUNT = 2;
    static readonly ConcurrentBag<QueuedUpload> queuedUploads = [];

    static readonly PooledList<Thread> threads = new();
    static readonly PooledList<NativeWindow> contexts = new();

    public static void Initialize()
    {
        Native.Window.Context.MakeNoneCurrent();

        for (var i = 0; i < UPLOAD_THREAD_COUNT; ++i)
        {
            NativeWindow window = new(new()
            {
                Flags = Native.Window.Flags,
                StartVisible = false,
                SharedContext = Native.Window.Context,
                DepthBits = 0,
                StencilBits = 0,
                AlphaBits = 0
            });

            contexts.Add(window);

            window.Context.MakeNoneCurrent();

            Thread thread = new(context =>
            {
                ((IGLFWGraphicsContext)context)?.MakeCurrent();

                Trace.WriteLine("Started texture upload thread");

                DecoderOptions decoderOptions = new() { Configuration = Configuration.Default.Clone() };
                decoderOptions.Configuration.PreferContiguousImageBuffers = true;

                while (!Native.Window.IsExiting)
                {
                    if (!queuedUploads.TryTake(out var queued)) continue;

                    var filename = queued.FileName;

                    using var stream = File.Exists(filename) ?
                        File.OpenRead(filename) :
                        queued.Container?.GetStream(filename, ResourceSource.Embedded);

                    if (stream is null)
                    {
                        Trace.TraceWarning($"Texture not found: {filename}");
                        continue;
                    }

                    using (var bitmap = Image.Load<Rgba32>(decoderOptions, stream))
                        queued.Result = Texture2d.Load(bitmap, queued.Options);

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
        queuedUploads.Add(toQueue);
        return toQueue;
    }

    public record QueuedUpload(string FileName, ResourceContainer Container, TextureOptions Options)
    {
        public bool IsLoaded;
        public Texture2d Result;
    }
}