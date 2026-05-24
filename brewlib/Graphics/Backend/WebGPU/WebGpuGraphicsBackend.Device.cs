namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Text;
using System.Threading;
using SDL3;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;

public unsafe sealed partial class WebGpuGraphicsBackend
{
    void createInstanceAndAdapter()
    {
        instance = createInstance();
        if (instance is null)
            throw new InvalidOperationException("Unable to create WebGPU instance");

        if (windowHandle != 0)
            surface = createSurface(windowHandle);

        adapter = requestAdapter();
        if (adapter is null)
            throw new InvalidOperationException("Unable to acquire WebGPU adapter");
    }

    Instance* createInstance()
    {
        InstanceDescriptor instanceDescriptor = new();
        return Api.CreateInstance(&instanceDescriptor);
    }

    Adapter* requestAdapter()
    {
        using ManualResetEventSlim ready = new();
        Adapter* requestedAdapter = null;

        requestAdapterCallback = new((status, receivedAdapter, _, _) =>
        {
            if (status == RequestAdapterStatus.Success)
                requestedAdapter = receivedAdapter;

            ready.Set();
        });

        RequestAdapterOptions options = new()
        {
            CompatibleSurface = surface
        };

        Api.InstanceRequestAdapter(instance, in options, requestAdapterCallback, null);
        ready.Wait();
        return requestedAdapter;
    }

    Device* requestDevice()
    {
        var requestNativeNonUniformTextureIndexing =
            PreferNativeTextureBindingArrays && supportsAdapterNativeNonUniformTextureIndexing();

        var requestedNativeTextureBindings = 0;
        var requestedPartiallyBoundTextureBindings = false;
        Device* requestedDevice = null;

        if (requestNativeNonUniformTextureIndexing)
        {
            var adapterSupportsPartiallyBoundTextureBindings = supportsAdapterPartiallyBoundTextureBindingArrays();
            requestedDevice = tryRequestDeviceWithNativeTextureArrays(adapterSupportsPartiallyBoundTextureBindings,
                out requestedNativeTextureBindings);

            requestedPartiallyBoundTextureBindings =
                requestedDevice is not null && adapterSupportsPartiallyBoundTextureBindings;

            if (requestedDevice is null && adapterSupportsPartiallyBoundTextureBindings)
            {
                requestedDevice = tryRequestDeviceWithNativeTextureArrays(false,
                    out requestedNativeTextureBindings);

                requestedPartiallyBoundTextureBindings = false;
            }

            if (requestedDevice is null)
                SDL.LogWarn(LogCategory.Render,
                    "WebGPU adapter reports native texture binding arrays, but device creation failed with them enabled; retrying without them");
        }

        if (requestedDevice is null)
            requestedDevice = requestDevice(0);

        if (requestedDevice is null)
            throw new InvalidOperationException("Unable to acquire WebGPU device");

        supportsNativeNonUniformTextureIndexing = deviceSupportsNativeNonUniformTextureIndexing(requestedDevice);
        UseNativeNonUniformTextureIndexing = requestedNativeTextureBindings > 0 && supportsNativeNonUniformTextureIndexing;
        UsePartiallyBoundNativeTextureArrays =
            UseNativeNonUniformTextureIndexing &&
            requestedPartiallyBoundTextureBindings &&
            Api.DeviceHasFeature(requestedDevice,
                WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeaturePartiallyBoundBindingArray));

        return requestedDevice;
    }

    Device* tryRequestDeviceWithNativeTextureArrays(bool requestPartiallyBoundTextureBindings,
        out int requestedNativeTextureBindings)
    {
        requestedNativeTextureBindings = 0;

        var nativeTextureBindings = chooseNativeTextureBindingCapacity();
        while (nativeTextureBindings > MaxFragmentTextureBindings)
        {
            var requestedDevice = requestDevice(nativeTextureBindings,
                requestPartiallyBoundTextureBindings);

            if (requestedDevice is not null)
            {
                requestedNativeTextureBindings = nativeTextureBindings;
                return requestedDevice;
            }

            if (nativeTextureBindings <= MaxFragmentTextureBindings * 2)
                break;

            nativeTextureBindings = Math.Max(MaxFragmentTextureBindings + 1, nativeTextureBindings / 2);
        }

        return null;
    }

    int chooseNativeTextureBindingCapacity()
    {
        SupportedLimits supportedLimits = default;
        Api.AdapterGetLimits(adapter, ref supportedLimits);

        var maxSampledTextures = supportedLimits.Limits.MaxSampledTexturesPerShaderStage;
        if (maxSampledTextures <= MaxFragmentTextureBindings || maxSampledTextures == uint.MaxValue)
            maxSampledTextures = MaxNativeFragmentTextureBindings;

        return Math.Max(1, (int)Math.Min(MaxNativeFragmentTextureBindings, maxSampledTextures));
    }

    bool supportsAdapterNativeNonUniformTextureIndexing()
        => Api.AdapterHasFeature(adapter,
                WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeatureTextureBindingArray)) &&
            Api.AdapterHasFeature(adapter,
                WebGpuNativeExtensions.NativeFeature(
                    WebGpuNativeExtensions.FeatureSampledTextureAndStorageBufferArrayNonUniformIndexing));

    bool supportsAdapterPartiallyBoundTextureBindingArrays()
        => Api.AdapterHasFeature(adapter,
            WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeaturePartiallyBoundBindingArray));

    bool deviceSupportsNativeNonUniformTextureIndexing(Device* requestedDevice)
        => Api.DeviceHasFeature(requestedDevice,
                WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeatureTextureBindingArray)) &&
            Api.DeviceHasFeature(requestedDevice,
                WebGpuNativeExtensions.NativeFeature(
                    WebGpuNativeExtensions.FeatureSampledTextureAndStorageBufferArrayNonUniformIndexing));

    Device* requestDevice(int nativeTextureBindingCapacity,
        bool requestPartiallyBoundTextureBindings = false)
    {
        using ManualResetEventSlim ready = new();
        Device* requestedDevice = null;
        var requestedFeatures = stackalloc FeatureName[3];
        var requestedFeatureCount = 0;
        if (nativeTextureBindingCapacity != 0)
        {
            requestedFeatures[requestedFeatureCount++] =
                WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeatureTextureBindingArray);

            requestedFeatures[requestedFeatureCount++] = WebGpuNativeExtensions.NativeFeature(
                WebGpuNativeExtensions.FeatureSampledTextureAndStorageBufferArrayNonUniformIndexing);

            if (requestPartiallyBoundTextureBindings)
                requestedFeatures[requestedFeatureCount++] =
                    WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeaturePartiallyBoundBindingArray);
        }

        var requiredLimits = createUndefinedRequiredLimits();
        if (nativeTextureBindingCapacity > MaxFragmentTextureBindings)
            requiredLimits.Limits.MaxSampledTexturesPerShaderStage = (uint)nativeTextureBindingCapacity;

        requestDeviceCallback = new((status, receivedDevice, _, _) =>
        {
            if (status == RequestDeviceStatus.Success)
                requestedDevice = receivedDevice;

            ready.Set();
        });

        DeviceDescriptor descriptor = new()
        {
            DeviceLostCallback = deviceLostCallback,
            RequiredFeatureCount = (nuint)requestedFeatureCount,
            RequiredFeatures = requestedFeatureCount != 0 ? requestedFeatures : null,
            RequiredLimits = nativeTextureBindingCapacity > MaxFragmentTextureBindings ? &requiredLimits : null
        };

        Api.AdapterRequestDevice(adapter, in descriptor, requestDeviceCallback, null);
        ready.Wait();

        return requestedDevice;
    }

    static RequiredLimits createUndefinedRequiredLimits()
        => new()
        {
            Limits = new()
            {
                MaxTextureDimension1D = uint.MaxValue,
                MaxTextureDimension2D = uint.MaxValue,
                MaxTextureDimension3D = uint.MaxValue,
                MaxTextureArrayLayers = uint.MaxValue,
                MaxBindGroups = uint.MaxValue,
                MaxBindGroupsPlusVertexBuffers = uint.MaxValue,
                MaxBindingsPerBindGroup = uint.MaxValue,
                MaxDynamicUniformBuffersPerPipelineLayout = uint.MaxValue,
                MaxDynamicStorageBuffersPerPipelineLayout = uint.MaxValue,
                MaxSampledTexturesPerShaderStage = uint.MaxValue,
                MaxSamplersPerShaderStage = uint.MaxValue,
                MaxStorageBuffersPerShaderStage = uint.MaxValue,
                MaxStorageTexturesPerShaderStage = uint.MaxValue,
                MaxUniformBuffersPerShaderStage = uint.MaxValue,
                MaxUniformBufferBindingSize = ulong.MaxValue,
                MaxStorageBufferBindingSize = ulong.MaxValue,
                MinUniformBufferOffsetAlignment = uint.MaxValue,
                MinStorageBufferOffsetAlignment = uint.MaxValue,
                MaxVertexBuffers = uint.MaxValue,
                MaxBufferSize = ulong.MaxValue,
                MaxVertexAttributes = uint.MaxValue,
                MaxVertexBufferArrayStride = uint.MaxValue,
                MaxInterStageShaderComponents = uint.MaxValue,
                MaxInterStageShaderVariables = uint.MaxValue,
                MaxColorAttachments = uint.MaxValue,
                MaxColorAttachmentBytesPerSample = uint.MaxValue,
                MaxComputeWorkgroupStorageSize = uint.MaxValue,
                MaxComputeInvocationsPerWorkgroup = uint.MaxValue,
                MaxComputeWorkgroupSizeX = uint.MaxValue,
                MaxComputeWorkgroupSizeY = uint.MaxValue,
                MaxComputeWorkgroupSizeZ = uint.MaxValue,
                MaxComputeWorkgroupsPerDimension = uint.MaxValue
            }
        };

    void loadDeviceLimits()
    {
        SupportedLimits supportedLimits = default;
        Api.DeviceGetLimits(DeviceHandle, ref supportedLimits);

        var l = supportedLimits.Limits;
        var reportedMaxBufferSize = l.MaxBufferSize;
        if (reportedMaxBufferSize != 0)
            MaxBufferSize = (int)Math.Min(reportedMaxBufferSize, int.MaxValue);

        if (UseNativeNonUniformTextureIndexing)
        {
            var nativeTextureBindings = l.MaxSampledTexturesPerShaderStage != 0 &&
                l.MaxSampledTexturesPerShaderStage != uint.MaxValue
                    ? (int)Math.Min(MaxNativeFragmentTextureBindings, l.MaxSampledTexturesPerShaderStage)
                    : MaxFragmentTextureBindings;

            maxFragmentTextureBindings = Math.Max(1, nativeTextureBindings);
        }
        else if (l.MaxSampledTexturesPerShaderStage != 0)
            maxFragmentTextureBindings = (int)Math.Min(l.MaxSampledTexturesPerShaderStage, MaxFragmentTextureBindings);
    }

    void rebuildCapabilities()
    {
        var features = GraphicsBackendFeatures.TextureAtlases |
            GraphicsBackendFeatures.Instancing |
            GraphicsBackendFeatures.ComputeShaders;

        if (UseNativeNonUniformTextureIndexing)
            features |= GraphicsBackendFeatures.NativeNonUniformTextureIndexing;

        Capabilities = new(features,
            16384,
            maxFragmentTextureBindings,
            0,
            0,
            maxFragmentTextureBindings,
            64 * 1024);
    }

    void logBackendCapabilities()
    {
        AdapterProperties properties = default;
        Api.AdapterGetProperties(adapter, ref properties);

        var name = SilkMarshal.PtrToString((nint)properties.Name) ?? "unknown";
        var vendor = SilkMarshal.PtrToString((nint)properties.VendorName) ?? "unknown";
        var driver = SilkMarshal.PtrToString((nint)properties.DriverDescription) ?? "unknown";
        SDL.LogInfo(LogCategory.Render,
            $"WebGPU adapter: {name} ({vendor}); backend: {properties.BackendType}; type: {properties.AdapterType}; driver: {driver}");

        var featureCount = Api.DeviceEnumerateFeatures(DeviceHandle, null);
        if (featureCount != 0)
        {
            var features = stackalloc FeatureName[(int)featureCount];
            Api.DeviceEnumerateFeatures(DeviceHandle, features);

            var sb = new StringBuilder("WebGPU features:");
            for (nuint i = 0; i < featureCount; ++i)
                sb.Append(' ').Append(formatFeatureName(features[i]));

            SDL.LogInfo(LogCategory.Render, sb.ToString());
        }

        SupportedLimits supportedLimits = default;
        Api.DeviceGetLimits(DeviceHandle, ref supportedLimits);
        var l = supportedLimits.Limits;
        SDL.LogInfo(LogCategory.Render,
            $"WebGPU limits: maxBufferSize={l.MaxBufferSize}; maxBindGroups={l.MaxBindGroups}; " +
            $"maxBindingsPerBindGroup={l.MaxBindingsPerBindGroup}; maxSampledTexturesPerStage={l.MaxSampledTexturesPerShaderStage}; " +
            $"maxSamplersPerStage={l.MaxSamplersPerShaderStage}; maxVertexBuffers={l.MaxVertexBuffers}; " +
            $"maxUniformBufferBindingSize={l.MaxUniformBufferBindingSize}; minUniformBufferOffsetAlignment={l.MinUniformBufferOffsetAlignment}");

        SDL.LogInfo(LogCategory.Render,
            UseNativeNonUniformTextureIndexing
                ? $"WebGPU texture binding mode: native array capacity={maxFragmentTextureBindings}; partiallyBound={UsePartiallyBoundNativeTextureArrays}"
                : $"WebGPU texture binding mode: core slots={maxFragmentTextureBindings}");
    }

    static string formatFeatureName(FeatureName feature)
    {
        if (feature == WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeatureTextureBindingArray))
            return "TextureBindingArray";

        if (feature == WebGpuNativeExtensions.NativeFeature(
            WebGpuNativeExtensions.FeatureSampledTextureAndStorageBufferArrayNonUniformIndexing))
            return "SampledTextureAndStorageBufferArrayNonUniformIndexing";

        if (feature == WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeaturePartiallyBoundBindingArray))
            return "PartiallyBoundBindingArray";

        return feature.ToString();
    }
}