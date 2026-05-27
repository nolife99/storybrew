namespace BrewLib.Graphics.Backend.WebGPU;

using Ahjo.Wgpu;

public readonly struct WebGpuResourceReference(nint handle)
{
    public readonly nint Handle = handle;
    public bool IsNull => Handle == 0;

    public static WebGpuResourceReference Of(TextureView textureView) => new(textureView.NativeHandle());
    public static WebGpuResourceReference Of(Sampler sampler) => new(sampler.NativeHandle());
    public static WebGpuResourceReference Of(Buffer buffer) => new(buffer.NativeHandle());
    public static WebGpuResourceReference Of(BindGroup bindGroup) => new(bindGroup.NativeHandle());
    public static WebGpuResourceReference Of(RenderPipeline pipeline) => new(pipeline.NativeHandle());
}