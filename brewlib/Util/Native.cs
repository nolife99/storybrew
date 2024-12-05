namespace BrewLib.Util;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using Image = OpenTK.Windowing.GraphicsLibraryFramework.Image;

public static unsafe class Native
{
    public static nint AllocateMemory(int cb) => (nint)NativeMemory.Alloc((nuint)cb);
    public static void FreeMemory(nint addr) => NativeMemory.Free((void*)addr);

    sealed class UnmanagedMemoryAllocator : MemoryAllocator
    {
        protected override int GetBufferCapacityInBytes() => int.MaxValue;
        public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
            => new UnmanagedBuffer<T>(length, options);
    }

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

public sealed unsafe class UnmanagedBuffer<T>(int length, AllocationOptions options = AllocationOptions.None)
    : MemoryManager<T> where T : struct
{
    readonly void* ptr = options is AllocationOptions.None ?
        NativeMemory.Alloc((nuint)length, (nuint)Marshal.SizeOf<T>()) :
        NativeMemory.AllocZeroed((nuint)length, (nuint)Marshal.SizeOf<T>());

    public nint Address => (nint)ptr;

    protected override void Dispose(bool disposing) => NativeMemory.Free(ptr);
    public override Span<T> GetSpan() => new(ptr, length);
    public override MemoryHandle Pin(int elementIndex = 0) => new(Unsafe.Add<T>(ptr, elementIndex));
    public override void Unpin() { }
}