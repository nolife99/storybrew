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

public sealed class UnmanagedBuffer<T> : MemoryManager<T> where T : struct
{
    readonly int length;

    public UnmanagedBuffer(int length, AllocationOptions options = AllocationOptions.None)
    {
        this.length = length;

        var byteCount = length * Marshal.SizeOf<T>();
        Address = Marshal.AllocHGlobal(byteCount);

        if (options is AllocationOptions.Clean)
            Unsafe.InitBlock(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), Address), 0, (uint)byteCount);
    }

    public nint Address { get; }

    protected override void Dispose(bool disposing) => Marshal.FreeHGlobal(Address);
    public override Span<T> GetSpan()
        => MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<T>(), Address), length);
    public override unsafe MemoryHandle Pin(int elementIndex = 0) => new(Unsafe.Add<T>((void*)Address, elementIndex));
    public override void Unpin() { }
}

public sealed class PooledManagedBuffer<T> : MemoryManager<T> where T : struct
{
    readonly int length;
    GCHandle pinHandle;

    public PooledManagedBuffer(int length, AllocationOptions options = AllocationOptions.None)
    {
        this.length = length;
        Buffer = ArrayPool<T>.Shared.Rent(length);

        if (options is AllocationOptions.Clean) Buffer.AsSpan(0, length).Clear();
    }

    public T[] Buffer { get; }

    protected override void Dispose(bool disposing) => ArrayPool<T>.Shared.Return(Buffer);
    public override Span<T> GetSpan() => new(Buffer, 0, length);
    public override unsafe MemoryHandle Pin(int elementIndex = 0)
    {
        if (!pinHandle.IsAllocated) pinHandle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
        return new(Unsafe.Add<T>((void*)pinHandle.AddrOfPinnedObject(), elementIndex), pinHandle, this);
    }
    public override void Unpin()
    {
        if (pinHandle.IsAllocated) pinHandle.Free();
    }
}