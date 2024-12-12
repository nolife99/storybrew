namespace BrewLib.Util;

using System;
using System.Windows.Media.Imaging;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.ImageSharp;
using Image = OpenTK.Windowing.GraphicsLibraryFramework.Image;

public static unsafe class Native
{
    #region Win32

    static nint handle;
    public static Window* GLFWPtr { get; private set; }

    public static nint MainWindowHandle => handle != 0 ? handle : throw new InvalidOperationException("hWnd isn't initialized");

    public static void InitializeHandle(NativeWindow glfwWindow)
    {
        Configuration.Default.MemoryAllocator = new UnmanagedMemoryAllocator();

        GLFWPtr = glfwWindow.WindowPtr;
        handle = GLFW.GetWin32Window(GLFWPtr);
    }

    public static void SetWindowIcon(Type type, string iconPath)
    {
        IconBitmapDecoder decoder;
        using (var iconResource = type.Assembly.GetManifestResourceStream(type, iconPath))
        {
            if (iconResource is null) return;
            decoder = new(iconResource, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }

        var frame = decoder.Frames[0];
        var bytesLength = frame.PixelWidth * frame.PixelHeight * 4;
        var bytes = stackalloc byte[bytesLength];

        frame.CopyPixels(default, (nint)bytes, bytesLength, frame.PixelWidth * 4);
        Image icon = new(frame.PixelWidth, frame.PixelHeight, bytes);
        GLFW.SetWindowIconRaw(GLFWPtr, 1, &icon);
    }

    #endregion
}