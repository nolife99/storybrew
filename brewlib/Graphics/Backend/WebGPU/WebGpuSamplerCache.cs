namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Generic;
using System.Threading;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using Textures;

sealed class WebGpuSamplerCache : IDisposable
{
    readonly string backendName;
    readonly Device device;
    readonly Dictionary<SamplerKey, Entry> entries = new();
    bool disposed;
    long nextHandleValue;

    public WebGpuSamplerCache(Device device, string backendName)
    {
        this.device = device;
        this.backendName = backendName;
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        foreach (var entry in entries.Values)
            entry.Sampler.Dispose();

        entries.Clear();
    }

    public Entry Get(TextureOptions options)
    {
        options ??= TextureOptions.Default;
        var key = SamplerKey.From(options);
        if (entries.TryGetValue(key, out var existing))
            return existing;

        var samplerDesc = new SamplerDescriptor
        {
            AddressModeU = WgpuMapper.ToWgpu(options.TextureWrapS),
            AddressModeV = WgpuMapper.ToWgpu(options.TextureWrapT),
            AddressModeW = WGPUAddressMode.ClampToEdge,
            MagFilter = WgpuMapper.ToWgpuFilter(options.TextureMagFilter).MinMag,
            MinFilter = WgpuMapper.ToWgpuFilter(options.TextureMinFilter).MinMag,
            MipmapFilter = WgpuMapper.ToWgpuFilter(options.TextureMinFilter).Mipmap,
            LodMinClamp = 0f,
            LodMaxClamp = key.HasMips ? 32f : 0f,
            Compare = WGPUCompareFunction.Undefined,
            MaxAnisotropy = 1
        };

        var sampler = device.CreateSampler(in samplerDesc);
        var entry = new Entry(sampler,
            new(backendName, (nint)Interlocked.Increment(ref nextHandleValue)),
            key);

        entries[key] = entry;
        return entry;
    }

    public readonly record struct Entry(Sampler Sampler, GraphicsResourceHandle Identity, SamplerKey Key);

    public readonly record struct SamplerKey(TextureFilter Min,
        TextureFilter Mag,
        TextureWrap WrapS,
        TextureWrap WrapT,
        bool HasMips)
    {
        public static SamplerKey From(TextureOptions options)
        {
            // The min filter's variant determines mip usage; mag filter doesn't choose mip behaviour in WGPU.
            var (_, _, wantsMips) = WgpuMapper.ToWgpuFilter(options.TextureMinFilter);
            return new(options.TextureMinFilter,
                options.TextureMagFilter,
                options.TextureWrapS,
                options.TextureWrapT,
                wantsMips);
        }
    }
}