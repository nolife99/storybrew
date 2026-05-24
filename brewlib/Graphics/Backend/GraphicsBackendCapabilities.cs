namespace BrewLib.Graphics.Backend;

using System;

[Flags]
public enum GraphicsBackendFeatures
{
    None = 0,
    SrgbFramebuffer = 1 << 0,
    TextureAtlases = 1 << 2,
    NonUniformTextureIndexing = 1 << 3,
    Instancing = 1 << 4,
    IndirectDraws = 1 << 5,
    ComputeShaders = 1 << 6,
    FramebufferInvalidation = 1 << 7,
    ImmutableBuffers = 1 << 8,
    ClearTexture = 1 << 9,
    VertexArrays = 1 << 10,
    NativeNonUniformTextureIndexing = 1 << 11,
    ManualColorCorrection = 1 << 12
}

public readonly record struct GraphicsBackendCapabilities(
    GraphicsBackendFeatures Features,
    int MaxTextureSize,
    int MaxTextureImageUnits,
    int MaxVertexTextureImageUnits,
    int MaxGeometryTextureImageUnits,
    int MaxCombinedTextureImageUnits,
    int MaxUniformBufferSize)
{
    public bool Has(GraphicsBackendFeatures feature) => (Features & feature) == feature;
}