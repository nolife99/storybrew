namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Reflection;
using System.Text;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using Tiny.PooledCollections.Generic.Temporary;

readonly struct WebGpuDeviceCapabilities
{
    public WebGpuDeviceCapabilities(
        WGPUFeatureName[] availableFeatures,
        WGPUFeatureName[] requiredFeatures,
        WGPULimits requiredLimits,
        WGPUNativeLimits nativeLimits,
        bool hasImmediates,
        bool hasTextureBindingArray,
        bool hasNonUniformIndexing,
        bool hasMultiDrawIndirect,
        bool hasBcCompression)
    {
        AvailableFeatures = availableFeatures;
        RequiredFeatures = requiredFeatures;
        RequiredLimits = requiredLimits;
        NativeLimits = nativeLimits;
        HasImmediates = hasImmediates;
        HasTextureBindingArray = hasTextureBindingArray;
        HasNonUniformIndexing = hasNonUniformIndexing;
        HasMultiDrawIndirect = hasMultiDrawIndirect;
        HasBcCompression = hasBcCompression;
    }

    public WGPUFeatureName[] AvailableFeatures { get; }
    public WGPUFeatureName[] RequiredFeatures { get; }
    public WGPULimits RequiredLimits { get; }
    public WGPUNativeLimits NativeLimits { get; }

    public bool HasImmediates { get; }
    public bool HasTextureBindingArray { get; }
    public bool HasNonUniformIndexing { get; }
    public bool HasMultiDrawIndirect { get; }
    public bool HasBcCompression { get; }

    public uint MaxImmediateSize => HasImmediates ? RequiredLimits.maxImmediateSize : 0u;

    public WebGpuDeviceCapabilities ResolveForCreatedDevice(Device device)
    {
        var deviceLimits = device.GetLimits();

        var hasImmediates = HasImmediates && deviceLimits.maxImmediateSize > 0u;
        if (!hasImmediates)
            deviceLimits.maxImmediateSize = 0u;

        return new(
            AvailableFeatures,
            RequiredFeatures,
            deviceLimits,
            NativeLimits,
            hasImmediates,
            HasTextureBindingArray,
            HasNonUniformIndexing,
            HasMultiDrawIndirect,
            HasBcCompression);
    }

    public string FormatLog(WGPULimits adapterLimits,
        WGPUTextureFormat surfaceFormat,
        bool srgbFramebuffer,
        bool manualColorCorrection)
    {
        var builder = new StringBuilder(2048);

        builder.AppendLine("WebGPU capabilities");
        builder.AppendLine("  Features");
        AppendFeatureList(builder, "available", AvailableFeatures);
        AppendFeatureList(builder, "requested", RequiredFeatures);

        builder.AppendLine("  Optional backend paths");
        builder.AppendLine($"    immediates: {(HasImmediates ? "enabled" : "disabled")} " +
            $"(created maxImmediateSize={RequiredLimits.maxImmediateSize}, adapter native maxImmediateSize={NativeLimits.maxImmediateSize})");

        builder.AppendLine($"    texture binding array: {(HasTextureBindingArray ? "enabled" : "disabled")}");
        builder.AppendLine($"    non-uniform indexing: {(HasNonUniformIndexing ? "enabled" : "disabled")}");
        builder.AppendLine($"    multi-draw indirect count: {(HasMultiDrawIndirect ? "enabled" : "disabled")}");
        builder.AppendLine($"    BC compression: {(HasBcCompression ? "enabled" : "disabled")}");

        builder.AppendLine("  Surface");
        builder.AppendLine($"    format: {surfaceFormat}");
        builder.AppendLine($"    sRGB framebuffer: {srgbFramebuffer}");
        builder.AppendLine($"    manual color correction: {manualColorCorrection}");

        builder.AppendLine("  Adapter limits");
        AppendFields(builder, adapterLimits, "    ");
        builder.AppendLine("  Created device limits");
        AppendFields(builder, RequiredLimits, "    ");
        builder.AppendLine("  Native adapter limits");
        AppendFields(builder, NativeLimits, "    ");

        return builder.ToString().TrimEnd();
    }

    static void AppendFeatureList(StringBuilder builder, string label, WGPUFeatureName[] features)
    {
        builder.Append("    ");
        builder.Append(label);
        builder.Append(": ");

        if (features is null || features.Length == 0)
        {
            builder.AppendLine("none");
            return;
        }

        for (var i = 0; i < features.Length; ++i)
        {
            if (i != 0) builder.Append(", ");
            builder.Append(Enum.GetName(features[i]));
        }

        builder.AppendLine();
    }

    static void AppendFields<T>(StringBuilder builder, T value, string indent)
    {
        var type = typeof(T);

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            builder.Append(indent);
            builder.Append(field.Name);
            builder.Append(": ");
            builder.Append(field.GetValue(value));
            builder.AppendLine();
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length != 0) continue;

            builder.Append(indent);
            builder.Append(property.Name);
            builder.Append(": ");
            builder.Append(property.GetValue(value));
            builder.AppendLine();
        }
    }

    public static WebGpuDeviceCapabilities Build(Adapter adapter)
    {
        var adapterLimits = adapter.GetLimits();
        var nativeLimits = adapter.GetNativeLimits();

        var supportedFeatures = adapter.GetSupportedFeatures();
        var availableFeatures = TempHashSet.Create(supportedFeatures);
        
        var requestedFeatures = TempList.Create<WGPUFeatureName>();

        var requiredLimits = adapterLimits;
        var hasImmediates = nativeLimits.maxImmediateSize > 0u &&
            TryRequest(ref availableFeatures, ref requestedFeatures, (WGPUFeatureName)WGPUNativeFeature.Immediates);

        requiredLimits.maxImmediateSize = hasImmediates ? nativeLimits.maxImmediateSize : 0u;

        var hasTextureBindingArray = TryRequest(ref availableFeatures,
            ref requestedFeatures,
            (WGPUFeatureName)WGPUNativeFeature.TextureBindingArray);

        var hasNonUniformIndexing = TryRequest(ref availableFeatures,
            ref requestedFeatures,
            (WGPUFeatureName)WGPUNativeFeature.SampledTextureAndStorageBufferArrayNonUniformIndexing);

        var hasMultiDrawIndirect = TryRequest(ref availableFeatures,
            ref requestedFeatures,
            (WGPUFeatureName)WGPUNativeFeature.MultiDrawIndirectCount);

        var hasBc = TryRequest(ref availableFeatures, ref requestedFeatures, WGPUFeatureName.TextureCompressionBC);

        TryRequest(ref availableFeatures, ref requestedFeatures, WGPUFeatureName.Float32Filterable);
        TryRequest(ref availableFeatures, ref requestedFeatures, WGPUFeatureName.TextureFormatsTier1);
        
        availableFeatures.Dispose();

        var reqFeatures = requestedFeatures.ToArray();
        requestedFeatures.Dispose();
        
        return new(
            supportedFeatures,
            reqFeatures,
            requiredLimits,
            nativeLimits,
            hasImmediates,
            hasTextureBindingArray,
            hasNonUniformIndexing,
            hasMultiDrawIndirect,
            hasBc);
    }

    static bool TryRequest(scoped ref TempHashSet<WGPUFeatureName> available, scoped ref TempList<WGPUFeatureName> requested, WGPUFeatureName feature)
    {
        if (!available.Contains(feature)) return false;

        requested.Add(feature);
        return true;
    }
}