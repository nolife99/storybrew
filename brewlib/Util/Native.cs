namespace BrewLib.Util;

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BrewLib.IO;
using SDL3;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;
using Image = SixLabors.ImageSharp.Image;

public static class Native
{
    static nint windowHandle;

    public static Func<Action<object>, object, ValueTask> MainThreadScheduler { get; set; }

    public static void InitializeHandle(nint window) => windowHandle = window;

    public static Task SetWindowIcon(ResourceContainer container, string iconPath)
    {
        var r = container.GetStream(iconPath, ResourceSource.Embedded);
        if (r is null) return Task.CompletedTask;

        return Task.Factory.StartNew(async streamObj =>
            {
                await using var iconResource = (Stream)streamObj;
                using var image = await Image.LoadAsync<Rgba32>(iconResource);
                image.Mutate(x => x.Resize(new(48), KnownResamplers.Triangle, false));

                using var bytes = ValueArray.Create<byte>(image.Width * image.Height * Unsafe.SizeOf<Rgba32>());
                image.CopyPixelDataTo(bytes.AsSpan());

                bytes.GetUnsafe(out var array, out _);

                var pinned = GCHandle.Alloc(array, GCHandleType.Pinned);
                var surface = SDL.CreateSurfaceFrom(image.Width,
                    image.Height,
                    SDL.PixelFormat.ABGR8888,
                    pinned.AddrOfPinnedObject(),
                    image.Width * 4);

                SDL.RunOnMainThread(x => SDL.SetWindowIcon(windowHandle, x), surface, true);

                SDL.DestroySurface(surface);
                pinned.Free();
            },
            r);
    }
}