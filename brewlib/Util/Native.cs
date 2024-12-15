namespace BrewLib.Util;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.ImageSharp;
using Image = OpenTK.Windowing.GraphicsLibraryFramework.Image;

public static unsafe class Native
{
    #region Win32

    static nint handle;
    static readonly UnmanagedMemoryAllocator allocator = new();

    public static Window* GLFWPtr { get; private set; }

    public static nint MainWindowHandle => handle != 0 ? handle : throw new InvalidOperationException("hWnd isn't initialized");

    public static void InitializeHandle(NativeWindow glfwWindow)
    {
        Configuration.Default.MemoryAllocator = allocator;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint AllocateMemory(int cb) => (nint)NativeMemory.Alloc((nuint)cb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint ReallocateMemory(nint ptr, int cb) => (nint)NativeMemory.Realloc((void*)ptr, (nuint)cb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FreeMemory(nint ptr) => NativeMemory.Free((void*)ptr);

    #endregion
}