namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Text;
using SDL3;

public sealed partial class WebGpuGraphicsBackend
{
    void loadDeviceLimits()
    {
        var limits = DeviceHandle.GetLimits();
        var reportedMaxBufferSize = limits.maxBufferSize;
        if (reportedMaxBufferSize != 0)
            MaxBufferSize = (int)Math.Min(reportedMaxBufferSize, int.MaxValue);

        if (limits.maxBindGroups != 0)
            MaxBindGroups = (int)Math.Min(limits.maxBindGroups, int.MaxValue);

        if (limits.maxBindingsPerBindGroup != 0)
            MaxBindingsPerBindGroup = (int)Math.Min(limits.maxBindingsPerBindGroup, int.MaxValue);

        if (limits.maxVertexBuffers != 0)
            MaxVertexBuffers = (int)Math.Min(limits.maxVertexBuffers, int.MaxValue);

        var textureSlots = MaxFragmentTextureBindings;
        if (limits.maxSampledTexturesPerShaderStage != 0)
            textureSlots = (int)Math.Min(textureSlots, limits.maxSampledTexturesPerShaderStage);

        if (limits.maxSamplersPerShaderStage != 0)
            textureSlots = (int)Math.Min(textureSlots, limits.maxSamplersPerShaderStage);

        if (limits.maxBindingsPerBindGroup != 0)
            textureSlots = (int)Math.Min(textureSlots, limits.maxBindingsPerBindGroup / 2);

        maxFragmentTextureBindings = int.Max(1, textureSlots);
    }

    void rebuildCapabilities()
    {
        var features = GraphicsBackendFeatures.TextureAtlases |
            GraphicsBackendFeatures.Instancing |
            GraphicsBackendFeatures.ComputeShaders;

        if (surfaceManager?.IsFormatSrgb == true)
            features |= GraphicsBackendFeatures.SrgbFramebuffer;

        Capabilities = new(features,
            16384,
            maxFragmentTextureBindings,
            0,
            0,
            maxFragmentTextureBindings,
            64 * 1024,
            MaxBindGroups,
            MaxBindingsPerBindGroup,
            MaxVertexBuffers);
    }

    void logBackendCapabilities()
    {
        var properties = adapter.GetInfo();
        SDL.LogInfo(LogCategory.Render,
            $"WebGPU adapter: {properties.Device} ({properties.Vendor}); backend: {properties.Backend}; type: {properties.Type}; driver: {properties.Description}");

        var features = DeviceHandle.GetFeatures();
        if (features.Length != 0)
        {
            var sb = new StringBuilder("WebGPU features:");
            foreach (var feature in features)
                sb.Append(' ').Append(feature);

            SDL.LogInfo(LogCategory.Render, sb.ToString());
        }

        var limits = DeviceHandle.GetLimits();
        SDL.LogInfo(LogCategory.Render,
            $"WebGPU limits: maxBufferSize={limits.maxBufferSize}; maxBindGroups={limits.maxBindGroups}; " +
            $"maxBindingsPerBindGroup={limits.maxBindingsPerBindGroup}; maxSampledTexturesPerStage={limits.maxSampledTexturesPerShaderStage}; " +
            $"maxSamplersPerStage={limits.maxSamplersPerShaderStage}; maxVertexBuffers={limits.maxVertexBuffers}; " +
            $"maxUniformBufferBindingSize={limits.maxUniformBufferBindingSize}; minUniformBufferOffsetAlignment={limits.minUniformBufferOffsetAlignment}");

        SDL.LogInfo(LogCategory.Render, $"WebGPU texture binding mode: core waterfall slots={maxFragmentTextureBindings}");
    }
}