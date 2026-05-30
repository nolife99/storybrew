namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Generic;
using Ahjo.Wgpu;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

sealed class WebGpuBindGroupCache : IDisposable
{
    readonly WebGpuBackend backend;
    readonly Device device;

    readonly int textureSlotCount;
    readonly bool textureArrayed;

    readonly Dictionary<IWebGpuTexture, List<TextureCacheKey>> textureBacklinks = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<TextureCacheKey, TextureCacheEntry> textureCache = new(EqualityComparer<TextureCacheKey>.Default);
    readonly BindGroupLayout textureLayout;

    readonly Dictionary<WebGpuGraphicsBuffer, List<UniformCacheKey>> uniformBacklinks = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<UniformCacheKey, UniformCacheEntry> uniformCache = new(EqualityComparer<UniformCacheKey>.Default);
    readonly BindGroupLayout uniformLayout;

    bool disposed;

    public WebGpuBindGroupCache(WebGpuBackend backend,
        Device device,
        BindGroupLayout textureLayout,
        uint textureGroupIndex,
        bool hasTextureGroup,
        int textureSlotCount,
        bool textureArrayed,
        BindGroupLayout uniformLayout,
        uint uniformGroupIndex,
        bool hasUniformGroup)
    {
        this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
        this.device = device;
        this.textureLayout = textureLayout;
        this.textureSlotCount = textureSlotCount;
        this.textureArrayed = textureArrayed;
        this.uniformLayout = uniformLayout;
        TextureGroupIndex = textureGroupIndex;
        UniformGroupIndex = uniformGroupIndex;
        HasTextureGroup = hasTextureGroup;
        HasUniformGroup = hasUniformGroup;
    }

    public uint TextureGroupIndex { get; }

    public uint UniformGroupIndex { get; }

    public bool HasTextureGroup { get; }

    public bool HasUniformGroup { get; }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;

        foreach (var (texture, _) in textureBacklinks)
            texture.Disposing -= OnTextureDisposing;

        textureBacklinks.Clear();

        foreach (var (buffer, _) in uniformBacklinks)
        {
            buffer.Disposing -= OnUniformBufferDisposing;
            buffer.Resized -= OnUniformBufferResized;
        }

        uniformBacklinks.Clear();

        foreach (var entry in textureCache.Values) Retire(entry.Group);
        textureCache.Clear();

        foreach (var entry in uniformCache.Values) Retire(entry.Group);
        uniformCache.Clear();
    }

    public BindGroup GetTextureBindGroup(scoped ReadOnlySpan<IWebGpuTexture> textures)
    {
        if (!HasTextureGroup)
            throw new InvalidOperationException("This pipeline has no texture bind group");

        if (textures.Length == 0)
            throw new ArgumentException("At least one texture is required", nameof(textures));

        if (textures.Length > textureSlotCount)
            throw new ArgumentException(
                $"Batch supplies {textures.Length} textures but the pipeline layout has only {textureSlotCount} slots",
                nameof(textures));

        var samplerIdentity = textures[0].SamplerIdentity;
        TextureCacheKey key = new(textures, samplerIdentity);

        if (textureCache.TryGetValue(key, out var existing))
            return existing.Group;

        var group = textureArrayed
            ? CreateArrayedBindGroup(textures)
            : CreateWaterfallBindGroup(textures);

        var entry = new TextureCacheEntry(group, textures);
        textureCache[key] = entry;

        for (var i = 0; i < textures.Length; ++i)
            AddTextureBacklink(textures[i], key);

        return group;
    }

    // Waterfall: the layout is fixed at textureSlotCount (texture, sampler) pairs, so every
    // binding must be supplied. Unused slots are padded with slot 0's view; the shader never
    // samples them (instances only reference assigned slots), and padding adds no new
    // lifetime since slot 0 is already a member of this set.
    BindGroup CreateWaterfallBindGroup(scoped ReadOnlySpan<IWebGpuTexture> textures)
    {
        var bindCount = textureSlotCount * 2;
        using var entries = TempArray.Create<BindGroupEntry>(bindCount);

        var sharedSampler = textures[0].SamplerEntry.Sampler;
        var padView = textures[0].View;
        for (var i = 0; i < textureSlotCount; ++i)
        {
            var view = i < textures.Length ? textures[i].View : padView;
            entries[i * 2] = BindGroupEntry.TextureView((uint)(i * 2), view);
            entries[i * 2 + 1] = BindGroupEntry.Sampler((uint)(i * 2 + 1), sharedSampler);
        }

        return device.CreateBindGroup(textureLayout, entries.AsReadOnlySpan());
    }

    // Bindless: one shared sampler at binding 1, and a binding_array of exactly textureSlotCount
    // views at binding 0. Every declared element is supplied — unused tail slots are padded with
    // slot 0's view. Padding adds no new lifetime (slot 0 is already a member of this set) and
    // those indices are never sampled (the slotter only hands out slots in [0, count)). Full
    // population is what keeps this safe on D3D12, whose STATIC descriptor ranges fault on any
    // uninitialized descriptor. Ahjo's CreateBindGroupBindless appends the array entry and chains
    // WGPUBindGroupEntryExtras carrying the view pointer + count.
    BindGroup CreateArrayedBindGroup(scoped ReadOnlySpan<IWebGpuTexture> textures)
    {
        var sharedSampler = textures[0].SamplerEntry.Sampler;

        using var views = TempArray.Create<TextureView>(textureSlotCount);
        var padView = textures[0].View;
        for (var i = 0; i < textureSlotCount; ++i)
            views[i] = i < textures.Length ? textures[i].View : padView;

        ReadOnlySpan<BindGroupEntry> samplerEntry = [BindGroupEntry.Sampler(1, sharedSampler)];

        return device.CreateBindGroupBindless(textureLayout,
            samplerEntry,
            0,
            views.AsReadOnlySpan());
    }

    public BindGroup GetUniformBindGroup(WebGpuGraphicsBuffer buffer, ulong bindingSize, bool dynamicOffset)
    {
        if (!HasUniformGroup)
            throw new InvalidOperationException("This pipeline has no uniform bind group");

        ArgumentNullException.ThrowIfNull(buffer);

        var key = new UniformCacheKey(buffer, buffer.Generation, bindingSize, dynamicOffset);
        if (uniformCache.TryGetValue(key, out var existing))
            return existing.Group;

        var group = device.CreateBindGroup(uniformLayout, [BindGroupEntry.Buffer(0, buffer.Buffer, 0, dynamicOffset ? bindingSize : 0)]);
        var entry = new UniformCacheEntry(group, buffer);
        uniformCache[key] = entry;

        if (!uniformBacklinks.TryGetValue(buffer, out var list))
        {
            list = new(2);
            uniformBacklinks[buffer] = list;

            buffer.Disposing += OnUniformBufferDisposing;
            buffer.Resized += OnUniformBufferResized;
        }

        list.Add(key);

        return group;
    }

    void OnTextureDisposing(IWebGpuTexture texture)
    {
        if (!textureBacklinks.TryGetValue(texture, out var list)) return;

        for (var i = 0; i < list.Count; ++i)
        {
            var key = list[i];
            if (!textureCache.Remove(key, out var entry)) continue;

            Retire(entry.Group);
            RemoveTextureBacklinksExcept(in entry, texture, in key);
        }

        list.Clear();
        textureBacklinks.Remove(texture);
        texture.Disposing -= OnTextureDisposing;
    }

    void OnUniformBufferDisposing(WebGpuGraphicsBuffer buffer) => EvictUniformsFor(buffer, true);

    void OnUniformBufferResized(WebGpuGraphicsBuffer buffer) => EvictUniformsFor(buffer, false);

    void EvictUniformsFor(WebGpuGraphicsBuffer buffer, bool unsubscribe)
    {
        if (!uniformBacklinks.TryGetValue(buffer, out var list)) return;

        for (var i = 0; i < list.Count; ++i)
        {
            var key = list[i];
            if (uniformCache.Remove(key, out var entry))
                Retire(entry.Group);
        }

        list.Clear();

        if (unsubscribe)
        {
            uniformBacklinks.Remove(buffer);
            buffer.Disposing -= OnUniformBufferDisposing;
            buffer.Resized -= OnUniformBufferResized;
        }
    }


    void AddTextureBacklink(IWebGpuTexture texture, in TextureCacheKey key)
    {
        if (!textureBacklinks.TryGetValue(texture, out var list))
        {
            list = new(2);
            textureBacklinks[texture] = list;
            texture.Disposing += OnTextureDisposing;
        }

        list.Add(key);
    }

    void RemoveTextureBacklinksExcept(in TextureCacheEntry entry, IWebGpuTexture except, in TextureCacheKey key)
    {
        RemoveTextureBacklink(entry.T0, except, in key);
        RemoveTextureBacklink(entry.T1, except, in key);
        RemoveTextureBacklink(entry.T2, except, in key);
        RemoveTextureBacklink(entry.T3, except, in key);

        if (entry.Extras is null) return;
        foreach (var texture in entry.Extras)
            RemoveTextureBacklink(texture, except, in key);
    }

    void RemoveTextureBacklink(IWebGpuTexture texture, IWebGpuTexture except, in TextureCacheKey key)
    {
        if (texture is null || ReferenceEquals(texture, except)) return;
        if (!textureBacklinks.TryGetValue(texture, out var list)) return;

        list.Remove(key);
        if (list.Count != 0) return;

        textureBacklinks.Remove(texture);
        texture.Disposing -= OnTextureDisposing;
    }

    void Retire(BindGroup group)
    {
        if (group.IsNull) return;

        backend.EnqueueDeferredDisposal(group);
    }


    readonly struct TextureCacheEntry
    {
        public readonly BindGroup Group;
        public readonly IWebGpuTexture T0, T1, T2, T3;
        public readonly IWebGpuTexture[] Extras;

        public TextureCacheEntry(BindGroup group, scoped ReadOnlySpan<IWebGpuTexture> textures)
        {
            Group = group;
            T0 = textures.Length > 0 ? textures[0] : null;
            T1 = textures.Length > 1 ? textures[1] : null;
            T2 = textures.Length > 2 ? textures[2] : null;
            T3 = textures.Length > 3 ? textures[3] : null;

            if (textures.Length > 4)
            {
                Extras = new IWebGpuTexture[textures.Length - 4];
                for (var i = 0; i < Extras.Length; ++i) Extras[i] = textures[i + 4];
            }
            else
            {
                Extras = null;
            }
        }
    }

    readonly struct UniformCacheEntry
    {
        public readonly BindGroup Group;
        public readonly WebGpuGraphicsBuffer Buffer;

        public UniformCacheEntry(BindGroup group, WebGpuGraphicsBuffer buffer)
        {
            Group = group;
            Buffer = buffer;
        }
    }

    readonly struct TextureCacheKey : IEquatable<TextureCacheKey>
    {
        readonly IWebGpuTexture _t0, _t1, _t2, _t3;
        readonly IWebGpuTexture[] _extras;
        readonly int _count;
        readonly GraphicsResourceHandle _sampler;

        public TextureCacheKey(scoped ReadOnlySpan<IWebGpuTexture> textures, GraphicsResourceHandle sampler)
        {
            _count = textures.Length;
            _sampler = sampler;
            _t0 = textures.Length > 0 ? textures[0] : null;
            _t1 = textures.Length > 1 ? textures[1] : null;
            _t2 = textures.Length > 2 ? textures[2] : null;
            _t3 = textures.Length > 3 ? textures[3] : null;
            if (textures.Length > 4)
            {
                _extras = new IWebGpuTexture[textures.Length - 4];
                for (var i = 0; i < _extras.Length; ++i) _extras[i] = textures[i + 4];
            }
            else
            {
                _extras = null;
            }
        }

        public bool Equals(TextureCacheKey other)
        {
            if (_count != other._count) return false;
            if (_sampler.Value != other._sampler.Value || !string.Equals(_sampler.BackendName, other._sampler.BackendName, StringComparison.Ordinal)) return false;
            if (!ReferenceEquals(_t0, other._t0)) return false;
            if (!ReferenceEquals(_t1, other._t1)) return false;
            if (!ReferenceEquals(_t2, other._t2)) return false;
            if (!ReferenceEquals(_t3, other._t3)) return false;
            if (_extras is null) return other._extras is null;
            if (other._extras is null || _extras.Length != other._extras.Length) return false;

            for (var i = 0; i < _extras.Length; ++i)
                if (!ReferenceEquals(_extras[i], other._extras[i]))
                    return false;

            return true;
        }

        public override bool Equals(object obj) => obj is TextureCacheKey other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(_count);
            hash.Add(_sampler.Value);
            hash.Add(_sampler.BackendName);
            hash.Add(RuntimeHelpers.Identity(_t0));
            hash.Add(RuntimeHelpers.Identity(_t1));
            hash.Add(RuntimeHelpers.Identity(_t2));
            hash.Add(RuntimeHelpers.Identity(_t3));
            if (_extras is not null)
                foreach (var t in _extras)
                    hash.Add(RuntimeHelpers.Identity(t));

            return hash.ToHashCode();
        }
    }

    readonly struct UniformCacheKey : IEquatable<UniformCacheKey>
    {
        readonly WebGpuGraphicsBuffer buffer;
        readonly int bufferGeneration;
        readonly ulong bindingSize;
        readonly bool dynamicOffset;

        public UniformCacheKey(WebGpuGraphicsBuffer buffer, int bufferGeneration, ulong bindingSize, bool dynamicOffset)
        {
            this.buffer = buffer;
            this.bufferGeneration = bufferGeneration;
            this.bindingSize = bindingSize;
            this.dynamicOffset = dynamicOffset;
        }

        public bool Equals(UniformCacheKey other)
            => ReferenceEquals(buffer, other.buffer) &&
               bufferGeneration == other.bufferGeneration &&
               bindingSize == other.bindingSize &&
               dynamicOffset == other.dynamicOffset;

        public override bool Equals(object obj) => obj is UniformCacheKey other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(RuntimeHelpers.Identity(buffer), bufferGeneration, bindingSize, dynamicOffset);
    }

    static class RuntimeHelpers
    {
        public static int Identity(object obj)
            => obj is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}