namespace BrewLib.Util;

using System;
using System.IO;
using System.Threading.Tasks;
using BrewLib.IO;
using SDL3;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

public static class Native
{
    static nint windowHandle;

    public static Func<Action<object>, object, ValueTask> MainThreadScheduler { get; set; }

    public static void InitializeHandle(nint window) => windowHandle = window;

    public static Task SetWindowIcon(ResourceContainer container, string iconPath)
    {
        var r = container.GetStream(iconPath, ResourceSource.Embedded);
        return r is null ?
            Task.CompletedTask :
            Task.Factory.StartNew(async streamObj =>
                {
                    await using var iconResource = (Stream)streamObj;
                    using var image = await Image.LoadAsync<Rgba32>(iconResource);

                    ref var surface = ref SDL.CreateSurface(image.Width, image.Height, SDL.PixelFormat.ABGR8888)
                        .AsRef<Surface>();

                    SDL.LockSurface(surface.AsPointer());
                    image.CopyPixelDataTo(surface.Pixels.AsSpan<Rgba32>(image.Width * image.Height));

                    SDL.RunOnMainThread(x => SDL.SetWindowIcon(windowHandle, x), surface.AsPointer(), true);

                    SDL.UnlockSurface(surface.AsPointer());
                    SDL.DestroySurface(surface.AsPointer());
                },
                r);
    }
}