namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Ahjo.Wgpu;
using Shaders;
using Textures;

sealed class WebGpuResourceSet : IResourceSet
{
    readonly WebGpuBindGroupCache cache;
    readonly WebGpuRenderPipeline pipeline;
    int textureCount;

    IWebGpuTexture[] textures = [];

    public WebGpuResourceSet(WebGpuRenderPipeline pipeline, WebGpuBindGroupCache cache)
    {
        this.pipeline = pipeline;
        this.cache = cache;
    }

    public void SetTextures(ShaderSamplerBinding slot, scoped ReadOnlySpan<ITexture> textures)
    {
        if (textures.Length == 0)
        {
            textureCount = 0;
            return;
        }

        if (this.textures.Length < textures.Length)
            this.textures = new IWebGpuTexture[textures.Length];

        for (var i = 0; i < textures.Length; ++i)
        {
            if (textures[i] is not IWebGpuTexture wgpuTexture)
            {
                if (textures[i] is ITextureRegion region && region.Texture is IWebGpuTexture inner)
                    wgpuTexture = inner;
                else
                    throw new InvalidOperationException("Texture is not from the WebGPU backend");
            }

            this.textures[i] = wgpuTexture;
        }

        textureCount = textures.Length;
    }

    public void Dispose()
    {
        // No-op: the bind-group cache owns the lifetime of every bind group
    }

    public (BindGroup Group, uint GroupIndex) ResolveBindGroup()
    {
        if (textureCount == 0)
            throw new InvalidOperationException("No textures were set on this resource set");

        var group = cache.GetTextureBindGroup(textures.AsSpan(0, textureCount));
        return (group, cache.TextureGroupIndex);
    }
}