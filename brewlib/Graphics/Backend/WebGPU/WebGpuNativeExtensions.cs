namespace BrewLib.Graphics.Backend.WebGPU;

using System.Runtime.InteropServices;
using Silk.NET.WebGPU;

static class WebGpuNativeExtensions
{
    public const int STypeBindGroupEntryExtras = 0x00030007;
    public const int STypeBindGroupLayoutEntryExtras = 0x00030008;
    public const int FeatureTextureBindingArray = 0x00030006;
    public const int FeatureSampledTextureAndStorageBufferArrayNonUniformIndexing = 0x00030007;
    public const int FeaturePartiallyBoundBindingArray = 0x0003000A;

    public static FeatureName NativeFeature(int feature)
        => (FeatureName)feature;

    public static SType NativeSType(int sType)
        => (SType)sType;
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct WgpuNativeBindGroupEntryExtras
{
    public ChainedStruct Chain;
    public Buffer** Buffers;
    public nuint BufferCount;
    public Sampler** Samplers;
    public nuint SamplerCount;
    public TextureView** TextureViews;
    public nuint TextureViewCount;
}

[StructLayout(LayoutKind.Sequential)]
struct WgpuNativeBindGroupLayoutEntryExtras
{
    public ChainedStruct Chain;
    public uint Count;
}
