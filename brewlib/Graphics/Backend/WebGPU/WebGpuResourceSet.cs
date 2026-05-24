namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Concurrent;
using System.Numerics;
using Silk.NET.WebGPU;
using Textures;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using PipelineLayout = PipelineLayout;

public unsafe sealed class WebGpuResourceSet : IResourceSet
{
    const int MaxCachedBindGroups = 2048;

    readonly WebGpuGraphicsBackend backend;
    readonly ResourceBinding[] bindings;
    readonly ConcurrentQueue<PurgeRequest> pendingPurges = new();
    readonly WebGpuRenderPipeline pipeline;
    bool disposed;

    public WebGpuResourceSet(WebGpuGraphicsBackend backend,
        WebGpuRenderPipeline pipeline,
        PipelineLayout layout)
    {
        this.backend = backend;
        this.pipeline = pipeline;

        var pipelineTextureBindings = layout.TextureBindings;
        bindings = new ResourceBinding[pipelineTextureBindings.Length];
        for (var i = 0; i < pipelineTextureBindings.Length; ++i)
        {
            var binding = pipelineTextureBindings[i];
            bindings[i] = new(binding.Binding,
                binding.Capacity,
                backend.UseNativeNonUniformTextureIndexing);
        }

        if (bindings.Length != 0)
            backend.RegisterTextureResourceSet(this);
    }

    public void SetTextures(int binding, scoped ReadOnlySpan<ITexture> textures)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        ref var resourceBinding = ref getBinding(binding);
        if (textures.Length > resourceBinding.Capacity)
            throw new ArgumentException(
                $"Texture binding {binding} accepts at most {resourceBinding.Capacity} textures",
                nameof(textures));

        if (textures.Length == 0)
        {
            resourceBinding.ClearCurrent();
            return;
        }

        var signature = createSignature(resourceBinding.Binding, textures.Length);
        var changed = resourceBinding.TextureCount != textures.Length;
        var previousHandleCount = resourceBinding.HandleCount;
        GraphicsResourceHandle samplerIdentity = default;

        if (backend.UseNativeNonUniformTextureIndexing)
        {
            for (var i = 0; i < textures.Length; ++i)
            {
                if (textures[i] is not WebGpuTexture texture)
                    throw new InvalidOperationException($"{nameof(WebGpuResourceSet)} can only bind WebGPU textures");

                if (i == 0)
                    samplerIdentity = texture.SamplerIdentity;
                else if (texture.SamplerIdentity != samplerIdentity)
                    throw new InvalidOperationException(
                        "Native WebGPU texture arrays require batches to be split by sampler state");

                var viewHandle = (nint)texture.TextureViewHandle;
                changed |= resourceBinding.Handles[i] != viewHandle;
                resourceBinding.Handles[i] = viewHandle;
                signature.Add(viewHandle);
            }

            var samplerHandle = (nint)((WebGpuTexture)textures[0]).SamplerHandle;
            changed |= resourceBinding.Handles[textures.Length] != samplerHandle;
            resourceBinding.Handles[textures.Length] = samplerHandle;
            signature.Add(samplerHandle);
            resourceBinding.HandleCount = textures.Length + 1;
        }
        else
        {
            for (var i = 0; i < textures.Length; ++i)
            {
                if (textures[i] is not WebGpuTexture texture)
                    throw new InvalidOperationException($"{nameof(WebGpuResourceSet)} can only bind WebGPU textures");

                var viewHandle = (nint)texture.TextureViewHandle;
                var samplerHandle = (nint)texture.SamplerHandle;
                var handleIndex = i * 2;

                changed |= resourceBinding.Handles[handleIndex] != viewHandle ||
                    resourceBinding.Handles[handleIndex + 1] != samplerHandle;

                resourceBinding.Handles[handleIndex] = viewHandle;
                resourceBinding.Handles[handleIndex + 1] = samplerHandle;

                signature.Add(viewHandle);
                signature.Add(samplerHandle);
            }

            resourceBinding.HandleCount = textures.Length * 2;
        }

        if (previousHandleCount > resourceBinding.HandleCount)
            Array.Clear(resourceBinding.Handles, resourceBinding.HandleCount, previousHandleCount - resourceBinding.HandleCount);

        resourceBinding.TextureCount = textures.Length;
        var finalSignature = signature.ToHashCode();
        if (changed || resourceBinding.Signature != finalSignature)
        {
            resourceBinding.Signature = finalSignature;
            resourceBinding.BindGroupDirty = true;
        }
    }

    public void Bind()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (bindings.Length == 0) return;

        drainPendingPurges();

        if (!backend.TryRequireRenderPass(out var renderPass)) return;

        pipeline.EnsureBound(renderPass);

        for (var i = 0; i < bindings.Length; ++i)
        {
            ref var binding = ref bindings[i];
            if (binding.TextureCount == 0) continue;

            if (binding.BindGroup is null || binding.BindGroupDirty || binding.BoundSignature != binding.Signature)
                bindOrCreateBindGroup(ref binding);

            backend.SetBindGroup(1, binding.BindGroup);

            binding.BoundSignature = binding.Signature;
            binding.BindGroupDirty = false;
        }
    }

    public void Dispose()
    {
        if (disposed) return;

        for (var i = 0; i < bindings.Length; ++i)
        {
            bindings[i].Dispose(backend);
            bindings[i] = default;
        }

        if (bindings.Length != 0)
            backend.UnregisterTextureResourceSet(this);

        disposed = true;
    }

    internal void PurgeCachedBindGroupsReferencing(TextureView* textureView, Sampler* sampler)
    {
        if (disposed) return;

        pendingPurges.Enqueue(new(textureView, sampler));
    }

    void drainPendingPurges()
    {
        while (pendingPurges.TryDequeue(out var purge))
            purgeCachedBindGroupsReferencing(purge.TextureView, purge.Sampler);
    }

    void purgeCachedBindGroupsReferencing(TextureView* textureView, Sampler* sampler)
    {
        for (var i = 0; i < bindings.Length; ++i)
        {
            ref var binding = ref bindings[i];
            if (binding.Cache is null) continue;

            var removedCurrent = binding.Cache.PurgeReferences(backend,
                textureView,
                sampler,
                binding.BindGroup);

            if (!removedCurrent) continue;

            binding.BindGroup = null;
            binding.BoundSignature = 0;
            binding.BindGroupDirty = true;
        }
    }

    void bindOrCreateBindGroup(ref ResourceBinding binding)
    {
        var cache = binding.Cache ??= new(MaxCachedBindGroups);
        if (cache.TryGet(binding.ActiveHandles,
            binding.TextureCount,
            binding.Signature,
            ++binding.AccessSerial,
            out var cachedBindGroup))
        {
            binding.BindGroup = cachedBindGroup;
            return;
        }

        binding.BindGroup = backend.UseNativeNonUniformTextureIndexing
            ? createNativeTextureArrayBindGroup(in binding)
            : createCoreTextureBindGroup(in binding);

        cache.Add(backend,
            binding.ActiveHandles,
            binding.TextureCount,
            binding.Signature,
            binding.BindGroup,
            ++binding.AccessSerial,
            binding.BindGroup);
    }

    BindGroup* createCoreTextureBindGroup(scoped ref readonly ResourceBinding binding)
    {
        var capacity = binding.Capacity;
        var handles = binding.Handles;
        var fallbackView = (TextureView*)handles[0];
        var fallbackSampler = (Sampler*)handles[1];
        Span<BindGroupEntry> entries = stackalloc BindGroupEntry[checked(capacity * 2)];

        for (var i = 0; i < capacity; ++i)
        {
            var handleIndex = i < binding.TextureCount ? i * 2 : 0;
            entries[i * 2] = new()
            {
                Binding = (uint)(i * 2),
                TextureView = i < binding.TextureCount ? (TextureView*)handles[handleIndex] : fallbackView
            };

            entries[i * 2 + 1] = new()
            {
                Binding = (uint)(i * 2 + 1),
                Sampler = i < binding.TextureCount ? (Sampler*)handles[handleIndex + 1] : fallbackSampler
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

            var bindGroup = backend.Api.DeviceCreateBindGroup(backend.DeviceHandle, in descriptor);
            return bindGroup is not null
                ? bindGroup
                : throw new InvalidOperationException("Unable to create WebGPU texture bind group");
        }
    }

    BindGroup* createNativeTextureArrayBindGroup(scoped ref readonly ResourceBinding binding)
    {
        var capacity = binding.Capacity;
        var textureViewCount = backend.UsePartiallyBoundNativeTextureArrays
            ? binding.TextureCount
            : capacity;

        var textureViews = stackalloc TextureView*[textureViewCount];
        var handles = binding.Handles;
        var fallbackView = (TextureView*)handles[0];

        for (var i = 0; i < textureViewCount; ++i)
            textureViews[i] = i < binding.TextureCount ? (TextureView*)handles[i] : fallbackView;

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
            Sampler = (Sampler*)handles[binding.TextureCount]
        };

        BindGroupDescriptor descriptor = new()
        {
            Layout = pipeline.TextureBindGroupLayout,
            EntryCount = 2,
            Entries = entries
        };

        var bindGroup = backend.Api.DeviceCreateBindGroup(backend.DeviceHandle, in descriptor);
        return bindGroup is not null
            ? bindGroup
            : throw new InvalidOperationException("Unable to create WebGPU texture array bind group");
    }

    ref ResourceBinding getBinding(int binding)
    {
        for (var i = 0; i < bindings.Length; ++i)
            if (bindings[i].Binding == binding)
                return ref bindings[i];

        throw new ArgumentException($"Texture binding {binding} is not part of this resource set", nameof(binding));
    }

    static HashCode createSignature(int binding, int count)
    {
        var signature = new HashCode();
        signature.Add(HashCode.Combine(binding, count));
        return signature;
    }

    readonly struct PurgeRequest
    {
        public readonly TextureView* TextureView;
        public readonly Sampler* Sampler;

        public PurgeRequest(TextureView* textureView, Sampler* sampler)
        {
            TextureView = textureView;
            Sampler = sampler;
        }
    }

    struct ResourceBinding
    {
        public ResourceBinding(int binding, int capacity, bool nativeTextureArray)
        {
            Binding = binding;
            Capacity = capacity;
            Handles = new nint[nativeTextureArray ? capacity + 1 : capacity * 2];
        }

        public readonly int Binding;
        public readonly int Capacity;
        public readonly nint[] Handles;
        public int TextureCount;
        public int HandleCount;
        public BindGroup* BindGroup;
        public WebGpuBindGroupCache Cache;
        public int Signature;
        public ulong AccessSerial;
        public int BoundSignature;
        public bool BindGroupDirty;
        public ReadOnlySpan<nint> ActiveHandles => Handles.AsSpan(0, HandleCount);

        public void ClearCurrent()
        {
            if (HandleCount != 0)
                Array.Clear(Handles, 0, HandleCount);

            HandleCount = 0;
            TextureCount = 0;
            Signature = 0;
            BoundSignature = 0;
            BindGroupDirty = true;
        }

        public void Dispose(WebGpuGraphicsBackend backend)
        {
            Cache?.Clear(backend);
            Cache = null;
            BindGroup = null;

            if (Handles is not null)
                Array.Clear(Handles);
        }
    }

    sealed class WebGpuBindGroupCache
    {
        readonly CachedBindGroup[] entries;
        readonly int mask, maxEntries;
        int count;

        public WebGpuBindGroupCache(int maxEntries)
        {
            this.maxEntries = int.Max(16, maxEntries);
            var tableCapacity = nextPowerOfTwo(this.maxEntries * 2);
            entries = new CachedBindGroup[tableCapacity];
            mask = tableCapacity - 1;
        }

        public bool TryGet(ReadOnlySpan<nint> handles,
            int textureCount,
            int signature,
            ulong accessSerial,
            out BindGroup* bindGroup)
        {
            bindGroup = null;
            if (count == 0) return false;

            var index = signature & mask;
            for (var probes = 0; probes < entries.Length; ++probes)
            {
                ref var entry = ref entries[index];
                if (entry.State == CacheEntryState.Empty)
                    return false;

                if (entry.State == CacheEntryState.Occupied &&
                    entry.Signature == signature &&
                    entry.TextureCount == textureCount &&
                    entry.Matches(handles))
                {
                    entry.LastUsedSerial = accessSerial;
                    bindGroup = entry.BindGroup;
                    return true;
                }

                index = index + 1 & mask;
            }

            return false;
        }

        public void Add(WebGpuGraphicsBackend backend,
            ReadOnlySpan<nint> handles,
            int textureCount,
            int signature,
            BindGroup* bindGroup,
            ulong accessSerial,
            BindGroup* currentBindGroup)
        {
            if (count >= maxEntries)
            {
                var evictIndex = findOldestEvictionIndex(currentBindGroup);
                retireEntry(backend, ref entries[evictIndex]);
                entries[evictIndex] = CachedBindGroup.Tombstone;
                --count;
            }

            var index = findInsertIndex(signature);
            ref var entry = ref entries[index];
            if (entry.State == CacheEntryState.Occupied)
            {
                retireEntry(backend, ref entry);
                --count;
            }

            entry = CachedBindGroup.Create(handles,
                textureCount,
                signature,
                bindGroup,
                accessSerial);

            ++count;
        }

        public bool PurgeReferences(WebGpuGraphicsBackend backend,
            TextureView* textureView,
            Sampler* sampler,
            BindGroup* currentBindGroup)
        {
            if (count == 0) return false;

            var removedCurrent = false;
            for (var i = 0; i < entries.Length; ++i)
            {
                ref var entry = ref entries[i];
                if (entry.State != CacheEntryState.Occupied ||
                    !entry.References(textureView, sampler))
                    continue;

                removedCurrent |= entry.BindGroup == currentBindGroup;
                retireEntry(backend, ref entry);
                entry = CachedBindGroup.Tombstone;
                --count;
            }

            return removedCurrent;
        }

        public void Clear(WebGpuGraphicsBackend backend)
        {
            if (count == 0) return;

            for (var i = 0; i < entries.Length; ++i)
            {
                ref var entry = ref entries[i];
                if (entry.State != CacheEntryState.Occupied) continue;

                retireEntry(backend, ref entry);
                entry = default;
            }

            count = 0;
        }

        int findInsertIndex(int signature)
        {
            var index = signature & mask;
            var firstTombstone = -1;

            for (var probes = 0; probes < entries.Length; ++probes)
            {
                ref var entry = ref entries[index];
                if (entry.State == CacheEntryState.Empty)
                    return firstTombstone >= 0 ? firstTombstone : index;

                if (entry.State == CacheEntryState.Tombstone && firstTombstone < 0)
                    firstTombstone = index;

                index = index + 1 & mask;
            }

            return firstTombstone >= 0 ? firstTombstone : 0;
        }

        int findOldestEvictionIndex(BindGroup* currentBindGroup)
        {
            var oldestIndex = -1;
            var oldestSerial = ulong.MaxValue;

            for (var i = 0; i < entries.Length; ++i)
            {
                ref var entry = ref entries[i];
                if (entry.State != CacheEntryState.Occupied || entry.BindGroup == currentBindGroup)
                    continue;

                if (oldestIndex >= 0 && entry.LastUsedSerial >= oldestSerial) continue;

                oldestIndex = i;
                oldestSerial = entry.LastUsedSerial;
            }

            if (oldestIndex >= 0) return oldestIndex;

            for (var i = 0; i < entries.Length; ++i)
                if (entries[i].State == CacheEntryState.Occupied)
                    return i;

            return 0;
        }

        static void retireEntry(WebGpuGraphicsBackend backend, ref CachedBindGroup entry)
        {
            backend.RetireBindGroup(entry.BindGroup);
            entry.ReturnSnapshot();
        }

        static int nextPowerOfTwo(int value)
            => (int)BitOperations.RoundUpToPowerOf2((uint)value);
    }

    struct CachedBindGroup
    {
        public static readonly CachedBindGroup Tombstone = new()
        {
            State = CacheEntryState.Tombstone
        };

        public CacheEntryState State;
        public int Signature;
        public BindGroup* BindGroup;
        public TempArrayInternals<nint> Handles;
        public int TextureCount;
        public ulong LastUsedSerial;

        public static CachedBindGroup Create(ReadOnlySpan<nint> handles,
            int textureCount,
            int signature,
            BindGroup* bindGroup,
            ulong accessSerial)
        {
            var snapshot = TempArray.Create(handles);
            return new()
            {
                State = CacheEntryState.Occupied,
                Signature = signature,
                BindGroup = bindGroup,
                Handles = snapshot.TransferOwner(),
                TextureCount = textureCount,
                LastUsedSerial = accessSerial
            };
        }

        public bool Matches(ReadOnlySpan<nint> handles)
        {
            if (Handles.Array is null || Handles.Length != handles.Length) return false;

            return Handles.Array.AsSpan(0, Handles.Length).SequenceEqual(handles);
        }

        public bool References(TextureView* textureView, Sampler* sampler)
        {
            if (Handles.Array is null) return false;

            var textureViewHandle = (nint)textureView;
            var samplerHandle = (nint)sampler;
            foreach (var handle in Handles.Array.AsSpan(0, Handles.Length))
            {
                if (textureView is not null && handle == textureViewHandle ||
                    sampler is not null && handle == samplerHandle)
                    return true;
            }

            return false;
        }

        public void ReturnSnapshot()
        {
            if (Handles.Array is null) return;

            Handles.Dispose();
            Handles = default;
        }
    }

    enum CacheEntryState : byte
    {
        Empty,
        Occupied,
        Tombstone
    }
}