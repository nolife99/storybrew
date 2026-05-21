namespace BrewLib.Graphics.Backend.WebGPU;

using System.Collections.Generic;
using Silk.NET.WebGPU;
using WgpuBuffer = Silk.NET.WebGPU.Buffer;
using WgpuRenderPipeline = Silk.NET.WebGPU.RenderPipeline;
using WgpuSampler = Silk.NET.WebGPU.Sampler;
using WgpuTexture = Silk.NET.WebGPU.Texture;
using WgpuTextureView = Silk.NET.WebGPU.TextureView;

sealed unsafe class WebGpuRetiredResources
{
    public readonly List<WebGpuHandle<BindGroup>> BindGroups = [];
    public readonly List<WebGpuHandle<WgpuBuffer>> Buffers = [];
    public readonly List<WebGpuHandle<WgpuRenderPipeline>> RenderPipelines = [];
    public readonly List<WebGpuHandle<WgpuSampler>> Samplers = [];
    public readonly List<WebGpuHandle<WgpuTexture>> Textures = [];
    public readonly List<WebGpuHandle<WgpuTextureView>> TextureViews = [];

    public uint ReleaseFrameSerial { get; set; }

    public void Release(WebGPU wgpu)
    {
        // Bind groups must be released first so their internal refs to textures/samplers/buffers
        // drop before we Release the underlying resources.
        foreach (var bindGroup in BindGroups)
            wgpu.BindGroupRelease(bindGroup.Pointer);

        foreach (var renderPipeline in RenderPipelines)
            wgpu.RenderPipelineRelease(renderPipeline.Pointer);

        foreach (var textureView in TextureViews)
            wgpu.TextureViewRelease(textureView.Pointer);

        foreach (var sampler in Samplers)
            wgpu.SamplerRelease(sampler.Pointer);

        foreach (var texture in Textures)
        {
            wgpu.TextureDestroy(texture.Pointer);
            wgpu.TextureRelease(texture.Pointer);
        }

        foreach (var buffer in Buffers)
        {
            wgpu.BufferDestroy(buffer.Pointer);
            wgpu.BufferRelease(buffer.Pointer);
        }
    }
}

readonly unsafe struct WebGpuHandle<T>(T* pointer) where T : unmanaged
{
    public readonly T* Pointer = pointer;
}
