namespace BrewLib.Util;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OpenTK.Windowing.Desktop;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = OpenTK.Windowing.Common.Input.Image;

public static class Native
{
    public static NativeWindow Window { get; private set; }

    public static Func<Action, Task> MainThreadScheduler { get; set; }

    public static void InitializeHandle(NativeWindow glfwWindow) => Window = glfwWindow;

    public static void SetWindowIcon(Type type, string iconPath)
    {
        Image<Rgba32> image;
        using (var iconResource = type.Assembly.GetManifestResourceStream(type, iconPath))
        {
            if (iconResource is null) return;

            image = SixLabors.ImageSharp.Image.Load<Rgba32>(iconResource);
            image.Mutate(x => x.Resize(new(48), KnownResamplers.Triangle, false));
        }

        var bytes = ArrayPool<byte>.Shared.Rent(image.Width * image.Height * Unsafe.SizeOf<Rgba32>());
        image.CopyPixelDataTo(bytes);

        Window.Icon = new(new Image(image.Width, image.Height, bytes));

        image.Dispose();
        ArrayPool<byte>.Shared.Return(bytes);
    }
}