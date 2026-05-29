namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Generic;
using Shaders;
using SixLabors.ImageSharp;
using Textures;

sealed class WebGpuDevice : IGraphicsDevice
{
    readonly WebGpuBackend backend;

    readonly Dictionary<ITexture, int> legacyTextureSlots = new(ReferenceEqualityComparer.Instance);
    int nextLegacySlot;

    public WebGpuDevice(WebGpuBackend backend) => this.backend = backend;

    public void InitializeTextureSlots(int textureSlotCount)
    {
        legacyTextureSlots.Clear();
        nextLegacySlot = 0;
    }

    public void ResetStateCache()
    {
        backend.ResetStateCache();
        legacyTextureSlots.Clear();
        nextLegacySlot = 0;
    }

    public void SetViewport(Rectangle viewport) => backend.SetViewport(viewport);

    public void SetScissor(Rectangle? region) => backend.SetScissor(region);

    public void SetBlendState(BlendingFactorState state) => backend.SetBlendState(state);

    public void UseProgram(int programId)
        => throw new NotSupportedException("The WebGPU backend uses render-pipeline objects, not legacy shader programs");

    public void ActivateVertexAttributes(VertexDeclaration declaration, Shader shader)
        => throw new NotSupportedException("The WebGPU backend uses render-pipeline objects, not legacy vertex attribute binding");

    public void DeactivateVertexAttributes(VertexDeclaration declaration, Shader shader)
        => throw new NotSupportedException("The WebGPU backend uses render-pipeline objects, not legacy vertex attribute binding");

    public int BindTexture(ITexture texture)
    {
        if (texture is null) return -1;
        if (legacyTextureSlots.TryGetValue(texture, out var slot)) return slot;

        slot = nextLegacySlot++;
        legacyTextureSlots[texture] = slot;
        return slot;
    }

    public void BindTextures(scoped ReadOnlySpan<ITexture> textures, scoped Span<int> textureUnits)
    {
        for (var i = 0; i < textures.Length && i < textureUnits.Length; ++i)
            textureUnits[i] = BindTexture(textures[i]);
    }

    public void UnbindTexture(ITexture texture)
    {
        if (texture is not null) legacyTextureSlots.Remove(texture);
    }

    public void Dispose()
    {
        legacyTextureSlots.Clear();
    }
}