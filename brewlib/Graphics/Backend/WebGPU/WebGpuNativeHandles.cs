namespace BrewLib.Graphics.Backend.WebGPU;

using System.Runtime.CompilerServices;
using Ahjo.Wgpu;
using WgpuBuffer = Ahjo.Wgpu.Buffer;

static class WebGpuNativeHandles
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint NativeHandle(this Texture texture)
        => Unsafe.BitCast<Texture, nint>(texture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint NativeHandle(this TextureView textureView)
        => Unsafe.BitCast<TextureView, nint>(textureView);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint NativeHandle(this Sampler sampler)
        => Unsafe.BitCast<Sampler, nint>(sampler);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint NativeHandle(this WgpuBuffer buffer)
        => Unsafe.BitCast<WgpuBuffer, nint>(buffer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint NativeHandle(this RenderPipeline pipeline)
        => Unsafe.BitCast<RenderPipeline, nint>(pipeline);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint NativeHandle(this BindGroup bindGroup)
        => Unsafe.BitCast<BindGroup, nint>(bindGroup);
}