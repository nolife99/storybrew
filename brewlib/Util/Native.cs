namespace BrewLib.Util;

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OpenTK.Windowing.Desktop;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public static class Native
{
    public static NativeWindow Window { get; private set; }

    public static Func<Action, Task> MainThreadScheduler { get; set; }

    public static void InitializeHandle(NativeWindow glfwWindow) => Window = glfwWindow;

    public static void SetWindowIcon(Type type, string iconPath)
    {
        using var iconResource = type.Assembly.GetManifestResourceStream(type, iconPath);
        if (iconResource is null) return;

        using var image = Image.Load<Rgba32>(iconResource);
        image.Mutate(x => x.Resize(new(48), KnownResamplers.Triangle, false));

        using var bytes = TempArray.Create<byte>(image.Width * image.Height * Unsafe.SizeOf<Rgba32>());
        image.CopyPixelDataTo(bytes.AsSpan());

        bytes.GetUnsafe(out var array, out _);
        Window.Icon = new(new OpenTK.Windowing.Common.Input.Image(image.Width, image.Height, array));
    }
}