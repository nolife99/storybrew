namespace BrewLib.Graphics.Backend.OpenGL;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SDL3;
using Silk.NET.OpenGL;

static class OpenGlApi
{
    public static GL GL { get; private set; }

    public static void Load()
    {
        GL ??= GL.GetApi(static name =>
        {
            var proc = SDL.GLGetProcAddress(name);
            return proc is null ? nint.Zero : Marshal.GetFunctionPointerForDelegate(proc);
        });
    }

    public static void AllocateBuffer(BufferTargetARB target, int sizeInBytes, BufferUsageARB usage)
        => GL.BufferData(target, (nuint)sizeInBytes, in Unsafe.NullRef<byte>(), usage);

    public static unsafe nint MapBufferRange(BufferTargetARB target, int offset, int sizeInBytes, MapBufferAccessMask flags)
        => (nint)GL.MapBufferRange(target, offset, (nuint)sizeInBytes, flags);

    public static void UploadTextureImage2D(TextureTarget target,
        int level,
        InternalFormat internalFormat,
        int width,
        int height,
        PixelFormat format,
        PixelType type)
        => GL.TexImage2D(target,
            level,
            internalFormat,
            (uint)width,
            (uint)height,
            0,
            format,
            type,
            in Unsafe.NullRef<byte>());

    public static void UploadTextureSubImage2D(TextureTarget target,
        int level,
        int x,
        int y,
        int width,
        int height,
        PixelFormat format,
        PixelType type)
        => GL.TexSubImage2D(target,
            level,
            x,
            y,
            (uint)width,
            (uint)height,
            format,
            type,
            in Unsafe.NullRef<byte>());

    public static void DrawElements(PrimitiveType primitiveType, int count, DrawElementsType elementType)
        => GL.DrawElements(primitiveType, (uint)count, elementType, in Unsafe.NullRef<byte>());
}