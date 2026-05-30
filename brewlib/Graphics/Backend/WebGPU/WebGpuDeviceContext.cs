namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Threading;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using Ahjo.Wgpu.Util;

sealed class WebGpuDeviceContext : IDisposable
{
    string _lastUncapturedError;

    bool disposed;

    public WebGpuDeviceContext(
        Instance instance,
        Adapter adapter,
        Device device,
        WGPULimits limits,
        WGPUNativeLimits nativeLimits,
        bool hasImmediates,
        bool hasTextureBindingArray,
        bool hasNonUniformIndexing,
        bool hasMultiDrawIndirect,
        bool hasBcCompression)
    {
        Instance = instance;
        Adapter = adapter;
        Device = device;
        Queue = device.Queue;
        Limits = limits;
        NativeLimits = nativeLimits;

        HasImmediates = hasImmediates;
        HasTextureBindingArray = hasTextureBindingArray;
        HasNonUniformIndexing = hasNonUniformIndexing;
        HasMultiDrawIndirect = hasMultiDrawIndirect;
        HasBcCompression = hasBcCompression;

        MipmapGenerator = new(device);
        TextureStager = new(this);

        MinUniformOffsetAlignment = limits.minUniformBufferOffsetAlignment == 0
            ? 256u
            : limits.minUniformBufferOffsetAlignment;

        MaxImmediateSize = hasImmediates ? limits.maxImmediateSize : 0u;
    }

    public Instance Instance { get; }
    public Adapter Adapter { get; }
    public Device Device { get; }
    public Queue Queue { get; }

    public WGPULimits Limits { get; }
    public WGPUNativeLimits NativeLimits { get; }

    public bool HasImmediates { get; }
    public bool HasTextureBindingArray { get; }
    public bool HasNonUniformIndexing { get; }
    public bool HasMultiDrawIndirect { get; }
    public bool HasBcCompression { get; }

    public uint MinUniformOffsetAlignment { get; }
    public uint MaxImmediateSize { get; }
    public bool CanUseImmediates => HasImmediates && MaxImmediateSize > 0u;

    public MipmapGenerator MipmapGenerator { get; }

    public WebGpuTextureStager TextureStager { get; }

    public void FlushQueuedWrites()
    {
        using var encoder = Device.CreateCommandEncoder();
        using var cmd = encoder.Finish();
        Queue.Submit(cmd);
    }

    public string LastUncapturedError
    {
        get => Volatile.Read(ref _lastUncapturedError);
        set => Volatile.Write(ref _lastUncapturedError, value);
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;

        TextureStager.Dispose();
        MipmapGenerator.Dispose();
        Device.Dispose();
        Adapter.Dispose();
    }
}