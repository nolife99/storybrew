namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Generic;
using Silk.NET.WebGPU;
using Textures;
using PipelineLayout = PipelineLayout;

public unsafe sealed class WebGpuResourceSet : IResourceSet
{
    const int MaxCachedBindGroups = 2048;
    const uint CachedBindGroupStaleFrames = 600;
    const uint CachePruneIntervalFrames = 60;

    readonly WebGpuGraphicsBackend backend;
    readonly WebGpuRenderPipeline pipeline;
    readonly TextureBinding[] textureBindings;
    bool disposed;

    public WebGpuResourceSet(WebGpuGraphicsBackend backend,
        WebGpuRenderPipeline pipeline,
        PipelineLayout layout)
    {
        this.backend = backend;
        this.pipeline = pipeline;

        var pipelineTextureBindings = layout.TextureBindings;
        textureBindings = new TextureBinding[pipelineTextureBindings.Length];
        for (var i = 0; i < pipelineTextureBindings.Length; ++i)
        {
            var binding = pipelineTextureBindings[i];
            textureBindings[i] = new(binding.Binding, binding.Capacity);
        }

        if (textureBindings.Length != 0)
            backend.RegisterTextureResourceSet(this);
    }

    public void SetTextures(int binding, scoped ReadOnlySpan<ITexture> textures)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        ref var textureBinding = ref getTextureBinding(binding);
        if (textures.Length > textureBinding.Textures.Length)
            throw new ArgumentException(
                $"Texture binding {binding} accepts at most {textureBinding.Textures.Length} textures",
                nameof(textures));

        var changed = textureBinding.Count != textures.Length;
        var signature = 14695981039346656037UL;
        GraphicsResourceHandle samplerIdentity = default;
        for (var i = 0; i < textures.Length; ++i)
        {
            if (textures[i] is not WebGpuTexture texture)
                throw new InvalidOperationException($"{nameof(WebGpuResourceSet)} can only bind WebGPU textures");

            if (backend.UseNativeNonUniformTextureIndexing)
            {
                if (i == 0)
                    samplerIdentity = texture.SamplerIdentity;
                else if (texture.SamplerIdentity != samplerIdentity)
                    throw new InvalidOperationException(
                        "Native WebGPU texture arrays require batches to be split by sampler state");
            }

            changed |= !ReferenceEquals(textureBinding.Textures[i], texture);
            textureBinding.Textures[i] = texture;
            signature = (signature ^ (ulong)(nint)texture.TextureViewHandle) * 1099511628211UL;
            if (!backend.UseNativeNonUniformTextureIndexing)
                signature = (signature ^ (ulong)(nint)texture.SamplerHandle) * 1099511628211UL;
        }

        if (backend.UseNativeNonUniformTextureIndexing && textures.Length != 0)
            signature = (signature ^ (ulong)samplerIdentity.Value) * 1099511628211UL;

        if (textureBinding.Count > textures.Length)
            Array.Clear(textureBinding.Textures, textures.Length, textureBinding.Count - textures.Length);

        textureBinding.Count = textures.Length;
        textureBinding.BindGroupDirty |= changed || textureBinding.Signature != (signature ^ (ulong)textures.Length);
        textureBinding.Signature = signature ^ (ulong)textures.Length;
    }

    public void Bind()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (textureBindings.Length == 0) return;

        if (!backend.TryRequireRenderPass(out var renderPass)) return;
        pipeline.EnsureBound(renderPass);

        var currentSerial = backend.RenderPassSerial;
        for (var i = 0; i < textureBindings.Length; ++i)
        {
            ref var binding = ref textureBindings[i];
            if (binding.Count == 0) continue;

            if (binding.CachedBindGroupCount != 0 &&
                unchecked(currentSerial - binding.LastCachePruneFrameSerial) >= CachePruneIntervalFrames)
            {
                pruneCachedBindGroups(ref binding, currentSerial, false);
                binding.LastCachePruneFrameSerial = currentSerial;
            }

            if (binding.BindGroup is null || binding.BindGroupDirty || binding.BoundSignature != binding.Signature)
                recreateBindGroup(ref binding);

            backend.SetBindGroup(1, binding.BindGroup);

            binding.BoundSerial = currentSerial;
            binding.BoundSignature = binding.Signature;
            binding.BindGroupDirty = false;
        }
    }

    public void Dispose()
    {
        if (disposed) return;

        for (var i = 0; i < textureBindings.Length; ++i)
        {
            textureBindings[i].Dispose(backend);
            textureBindings[i] = default;
        }

        if (textureBindings.Length != 0)
            backend.UnregisterTextureResourceSet(this);

        disposed = true;
    }

    internal void PurgeCachedBindGroupsReferencing(TextureView* textureView, Sampler* sampler)
    {
        if (disposed) return;

        for (var i = 0; i < textureBindings.Length; ++i)
            purgeCachedBindGroupsReferencing(ref textureBindings[i], textureView, sampler);
    }

    void recreateBindGroup(ref TextureBinding binding)
    {
        if (tryGetCachedBindGroup(ref binding, out var cachedBindGroup))
        {
            binding.BindGroup = cachedBindGroup;
            binding.BoundSerial = uint.MaxValue;
            return;
        }

        if (backend.UseNativeNonUniformTextureIndexing)
        {
            recreateNativeTextureArrayBindGroup(ref binding);
            return;
        }

        var textures = binding.Textures;
        var fallback = textures[0];
        Span<BindGroupEntry> entries = stackalloc BindGroupEntry[checked(textures.Length * 2)];
        for (var i = 0; i < textures.Length; ++i)
        {
            var texture = i < binding.Count ? textures[i] : fallback;
            entries[i * 2] = new()
            {
                Binding = (uint)(i * 2),
                TextureView = texture.TextureViewHandle
            };
            entries[i * 2 + 1] = new()
            {
                Binding = (uint)(i * 2 + 1),
                Sampler = texture.SamplerHandle
            };
        }

        fixed (BindGroupEntry* entriesPointer = entries)
        {
            BindGroupDescriptor descriptor = new()
            {
                Layout = pipeline.TextureBindGroupLayout,
                EntryCount = (nuint)entries.Length,
                Entries = entriesPointer
            };

            binding.BindGroup = backend.Api.DeviceCreateBindGroup(backend.DeviceHandle, in descriptor);
            if (binding.BindGroup is null)
                throw new InvalidOperationException("Unable to create WebGPU texture bind group");
        }

        cacheBindGroup(ref binding, binding.BindGroup);
        binding.BoundSerial = uint.MaxValue;
    }

    void recreateNativeTextureArrayBindGroup(ref TextureBinding binding)
    {
        var textures = binding.Textures;
        var capacity = textures.Length;
        var fallback = textures[0];
        var textureViewCount = backend.UsePartiallyBoundNativeTextureArrays
            ? binding.Count
            : capacity;
        var textureViews = stackalloc TextureView*[textureViewCount];

        for (var i = 0; i < textureViewCount; ++i)
        {
            var texture = i < binding.Count ? textures[i] : fallback;
            textureViews[i] = texture.TextureViewHandle;
        }

        WgpuNativeBindGroupEntryExtras textureExtras = new()
        {
            Chain = new()
            {
                SType = WebGpuNativeExtensions.NativeSType(
                    WebGpuNativeExtensions.STypeBindGroupEntryExtras)
            },
            TextureViews = textureViews,
            TextureViewCount = (nuint)textureViewCount
        };

        var entries = stackalloc BindGroupEntry[2];
        entries[0] = new()
        {
            NextInChain = &textureExtras.Chain,
            Binding = 0
        };
        entries[1] = new()
        {
            Binding = 1,
            Sampler = fallback.SamplerHandle
        };

        BindGroupDescriptor descriptor = new()
        {
            Layout = pipeline.TextureBindGroupLayout,
            EntryCount = 2,
            Entries = entries
        };

        binding.BindGroup = backend.Api.DeviceCreateBindGroup(backend.DeviceHandle, in descriptor);
        if (binding.BindGroup is null)
            throw new InvalidOperationException("Unable to create WebGPU texture array bind group");

        cacheBindGroup(ref binding, binding.BindGroup);
        binding.BoundSerial = uint.MaxValue;
    }

    bool tryGetCachedBindGroup(ref TextureBinding binding, out BindGroup* bindGroup)
    {
        bindGroup = null;
        if (binding.BindGroupCache is null ||
            !binding.BindGroupCache.TryGetValue(binding.Signature, out var cachedBindGroups))
            return false;

        for (var i = cachedBindGroups.Count - 1; i >= 0; --i)
        {
            var cached = cachedBindGroups[i];
            if (!cached.IsUsable)
            {
                removeCachedBindGroup(ref binding, cachedBindGroups, i);
                continue;
            }

            if (!cached.Matches(binding.Textures, binding.Count)) continue;

            cached.LastUsedFrameSerial = backend.FrameSerial;
            cachedBindGroups[i] = cached;
            bindGroup = cached.BindGroup;
            return true;
        }

        if (cachedBindGroups.Count == 0)
            binding.BindGroupCache.Remove(binding.Signature);

        return false;
    }

    void cacheBindGroup(ref TextureBinding binding, BindGroup* bindGroup)
    {
        var textureRefs = new WeakReference<WebGpuTexture>[binding.Count];
        var textureViews = new WebGpuHandle<TextureView>[binding.Count];
        var samplers = new WebGpuHandle<Sampler>[binding.Count];
        for (var i = 0; i < textureRefs.Length; ++i)
        {
            var texture = binding.Textures[i];
            textureRefs[i] = new(texture);
            textureViews[i] = new(texture.TextureViewHandle);
            samplers[i] = new(texture.SamplerHandle);
        }

        binding.BindGroupCache ??= [];
        if (!binding.BindGroupCache.TryGetValue(binding.Signature, out var cachedBindGroups))
            binding.BindGroupCache.Add(binding.Signature, cachedBindGroups = []);

        cachedBindGroups.Add(new(bindGroup, textureRefs, textureViews, samplers, backend.FrameSerial));
        ++binding.CachedBindGroupCount;

        pruneCachedBindGroups(ref binding, backend.FrameSerial, true);
    }

    void pruneCachedBindGroups(ref TextureBinding binding, uint frameSerial, bool enforceLimit)
    {
        if (binding.BindGroupCache is null || binding.CachedBindGroupCount == 0) return;

        List<ulong> emptySignatures = null;
        foreach (var pair in binding.BindGroupCache)
        {
            var cachedBindGroups = pair.Value;
            for (var i = cachedBindGroups.Count - 1; i >= 0; --i)
            {
                var cached = cachedBindGroups[i];
                if (cached.BindGroup == binding.BindGroup)
                    continue;
                if (cached.IsUsable &&
                    unchecked(frameSerial - cached.LastUsedFrameSerial) <= CachedBindGroupStaleFrames)
                    continue;

                removeCachedBindGroup(ref binding, cachedBindGroups, i);
            }

            if (cachedBindGroups.Count == 0)
                (emptySignatures ??= []).Add(pair.Key);
        }

        if (emptySignatures is not null)
            foreach (var signature in emptySignatures)
                binding.BindGroupCache.Remove(signature);

        if (!enforceLimit) return;

        while (binding.CachedBindGroupCount > MaxCachedBindGroups)
        {
            List<CachedBindGroup> oldestList = null;
            var oldestIndex = -1;
            var oldestAge = 0U;

            foreach (var cachedBindGroups in binding.BindGroupCache.Values)
            {
                for (var i = 0; i < cachedBindGroups.Count; ++i)
                {
                    var cached = cachedBindGroups[i];
                    if (cached.BindGroup == binding.BindGroup)
                        continue;

                    var age = unchecked(frameSerial - cached.LastUsedFrameSerial);
                    if (oldestList is not null && age <= oldestAge)
                        continue;

                    oldestList = cachedBindGroups;
                    oldestIndex = i;
                    oldestAge = age;
                }
            }

            if (oldestList is null) break;
            removeCachedBindGroup(ref binding, oldestList, oldestIndex);
        }
    }

    void purgeCachedBindGroupsReferencing(ref TextureBinding binding, TextureView* textureView, Sampler* sampler)
    {
        if (binding.BindGroupCache is null || binding.CachedBindGroupCount == 0) return;

        List<ulong> emptySignatures = null;
        foreach (var pair in binding.BindGroupCache)
        {
            var cachedBindGroups = pair.Value;
            for (var i = cachedBindGroups.Count - 1; i >= 0; --i)
            {
                var cached = cachedBindGroups[i];
                if (!cached.References(textureView, sampler)) continue;

                if (cached.BindGroup == binding.BindGroup)
                {
                    binding.BindGroup = null;
                    binding.BoundSignature = 0;
                    binding.BindGroupDirty = true;
                }

                removeCachedBindGroup(ref binding, cachedBindGroups, i);
            }

            if (cachedBindGroups.Count == 0)
                (emptySignatures ??= []).Add(pair.Key);
        }

        if (emptySignatures is not null)
            foreach (var signature in emptySignatures)
                binding.BindGroupCache.Remove(signature);
    }

    void removeCachedBindGroup(ref TextureBinding binding, List<CachedBindGroup> cachedBindGroups, int index)
    {
        var cached = cachedBindGroups[index];
        backend.RetireBindGroup(cached.BindGroup);
        cachedBindGroups.RemoveAt(index);
        --binding.CachedBindGroupCount;
    }

    ref TextureBinding getTextureBinding(int binding)
    {
        for (var i = 0; i < textureBindings.Length; ++i)
            if (textureBindings[i].Binding == binding)
                return ref textureBindings[i];

        throw new ArgumentException($"Texture binding {binding} is not part of this resource set", nameof(binding));
    }

    struct TextureBinding
    {
        public TextureBinding(int binding, int capacity)
        {
            Binding = binding;
            Textures = new WebGpuTexture[capacity];
            BoundSerial = uint.MaxValue;
            LastCachePruneFrameSerial = uint.MaxValue;
        }

        public int Binding;
        public WebGpuTexture[] Textures;
        public int Count;
        public BindGroup* BindGroup;
        public Dictionary<ulong, List<CachedBindGroup>> BindGroupCache;
        public int CachedBindGroupCount;
        public ulong Signature;
        public uint BoundSerial;
        public uint LastCachePruneFrameSerial;
        public ulong BoundSignature;
        public bool BindGroupDirty;

        public void Dispose(WebGpuGraphicsBackend backend)
        {
            if (BindGroupCache is not null)
            {
                foreach (var cachedBindGroups in BindGroupCache.Values)
                foreach (var cached in cachedBindGroups)
                    backend.RetireBindGroup(cached.BindGroup);

                BindGroupCache.Clear();
                CachedBindGroupCount = 0;
            }

            BindGroup = null;
            if (Textures is not null)
                Array.Clear(Textures);
        }
    }

    struct CachedBindGroup(BindGroup* bindGroup,
        WeakReference<WebGpuTexture>[] textures,
        WebGpuHandle<TextureView>[] textureViews,
        WebGpuHandle<Sampler>[] samplers,
        uint lastUsedFrameSerial)
    {
        public readonly BindGroup* BindGroup = bindGroup;
        readonly WeakReference<WebGpuTexture>[] textures = textures;
        readonly WebGpuHandle<TextureView>[] textureViews = textureViews;
        readonly WebGpuHandle<Sampler>[] samplers = samplers;
        public uint LastUsedFrameSerial = lastUsedFrameSerial;

        public bool IsUsable
        {
            get
            {
                for (var i = 0; i < textures.Length; ++i)
                {
                    if (!textures[i].TryGetTarget(out var texture)) return false;
                    if (texture.TextureViewHandle is null || texture.SamplerHandle is null) return false;
                }

                return true;
            }
        }

        public bool Matches(WebGpuTexture[] currentTextures, int count)
        {
            if (textureViews.Length != count) return false;
            for (var i = 0; i < count; ++i)
            {
                var texture = currentTextures[i];
                if (texture.TextureViewHandle != textureViews[i].Pointer ||
                    texture.SamplerHandle != samplers[i].Pointer)
                    return false;
            }

            return true;
        }

        public bool References(TextureView* textureView, Sampler* sampler)
        {
            for (var i = 0; i < textureViews.Length; ++i)
                if ((textureView is not null && textureViews[i].Pointer == textureView) ||
                    (sampler is not null && samplers[i].Pointer == sampler))
                    return true;

            return false;
        }
    }
}
