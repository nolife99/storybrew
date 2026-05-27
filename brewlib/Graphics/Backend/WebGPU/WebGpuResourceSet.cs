namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Ahjo.Wgpu;
using Textures;
using PipelineLayout = PipelineLayout;

public sealed class WebGpuResourceSet : IResourceSet, IWebGpuCachedResourceSet
{
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
                binding.Capacity);
        }

        if (bindings.Length != 0)
            backend.RegisterCachedResourceSet(this);
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
            resourceBinding.ClearCurrent(backend);
            return;
        }

        var signature = HashCode.Combine(resourceBinding.Binding, textures.Length);
        var changed = resourceBinding.TextureCount != textures.Length;
        var previousHandleCount = resourceBinding.HandleCount;
        for (var i = 0; i < textures.Length; ++i)
        {
            if (textures[i] is not WebGpuTexture texture)
                throw new InvalidOperationException($"{nameof(WebGpuResourceSet)} can only bind WebGPU textures");

            var viewHandle = texture.TextureViewHandle.NativeHandle();
            var samplerHandle = texture.SamplerHandle.NativeHandle();
            var handleIndex = i * 2;

            changed |= resourceBinding.Handles[handleIndex] != viewHandle ||
                resourceBinding.Handles[handleIndex + 1] != samplerHandle;

            resourceBinding.Handles[handleIndex] = viewHandle;
            resourceBinding.Handles[handleIndex + 1] = samplerHandle;
            resourceBinding.TextureViews[i] = texture.TextureViewHandle;
            resourceBinding.Samplers[i] = texture.SamplerHandle;

            signature = HashCode.Combine(signature, viewHandle, samplerHandle);
        }

        resourceBinding.HandleCount = textures.Length * 2;

        if (previousHandleCount > resourceBinding.HandleCount)
            Array.Clear(resourceBinding.Handles, resourceBinding.HandleCount, previousHandleCount - resourceBinding.HandleCount);

        resourceBinding.TextureCount = textures.Length;
        if (changed || resourceBinding.Signature != signature)
        {
            resourceBinding.Signature = signature;
            resourceBinding.BindGroupDirty = true;
        }
    }

    public void Bind()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (bindings.Length == 0) return;

        drainPendingPurges();

        if (!backend.TryRequireRenderPass()) return;

        for (var i = 0; i < bindings.Length; ++i)
        {
            ref var binding = ref bindings[i];
            if (binding.TextureCount == 0) continue;

            if (binding.BindGroup.IsNull || binding.BindGroupDirty || binding.BoundSignature != binding.Signature)
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
            backend.UnregisterCachedResourceSet(this);

        disposed = true;
    }

    public void PurgeCachedBindGroupsReferencing(WebGpuResourceReference resource)
    {
        if (disposed || resource.IsNull) return;

        pendingPurges.Enqueue(new(resource));
    }

    void drainPendingPurges()
    {
        while (pendingPurges.TryDequeue(out var purge))
            purgeCachedBindGroupsReferencing(purge.Resource);
    }

    void purgeCachedBindGroupsReferencing(WebGpuResourceReference resource)
    {
        for (var i = 0; i < bindings.Length; ++i)
        {
            ref var binding = ref bindings[i];
            if (!binding.PurgeCachedBindGroupsReferencing(backend, resource)) continue;

            binding.BoundSignature = 0;
            binding.BindGroupDirty = true;
        }
    }

    void bindOrCreateBindGroup(ref ResourceBinding binding)
    {
        var frameSerial = backend.FrameSerial;
        binding.TrimCache(backend, frameSerial);

        if (binding.TryGetCachedBindGroup(frameSerial, out var bindGroup))
        {
            binding.BindGroup = bindGroup;
            return;
        }

        bindGroup = createCoreTextureBindGroup(in binding);
        binding.CacheBindGroup(backend, bindGroup, frameSerial);
        binding.BindGroup = bindGroup;
    }

    BindGroup createCoreTextureBindGroup(scoped ref readonly ResourceBinding binding)
    {
        var capacity = binding.Capacity;
        var entryCount = checked(capacity * 2);
        var fallbackView = binding.TextureViews[0];
        var fallbackSampler = binding.Samplers[0];
        
        Span<BindGroupEntry> entries = stackalloc BindGroupEntry[entryCount];
        for (var i = 0; i < capacity; ++i)
        {
            entries[i * 2] = BindGroupEntry.TextureView((uint)(i * 2),
                i < binding.TextureCount ? binding.TextureViews[i] : fallbackView);

            entries[i * 2 + 1] = BindGroupEntry.Sampler((uint)(i * 2 + 1),
                i < binding.TextureCount ? binding.Samplers[i] : fallbackSampler);
        }

        return backend.DeviceHandle.CreateBindGroup(pipeline.TextureBindGroupLayout, entries);
    }

    ref ResourceBinding getBinding(int binding)
    {
        for (var i = 0; i < bindings.Length; ++i)
            if (bindings[i].Binding == binding)
                return ref bindings[i];

        throw new ArgumentException($"Texture binding {binding} is not part of this resource set", nameof(binding));
    }

    readonly struct PurgeRequest(WebGpuResourceReference resource)
    {
        public readonly WebGpuResourceReference Resource = resource;
    }

    struct ResourceBinding
    {
        public ResourceBinding(int binding, int capacity)
        {
            Binding = binding;
            Capacity = capacity;
            Handles = new nint[capacity * 2];
            TextureViews = new TextureView[capacity];
            Samplers = new Sampler[capacity];
            Cache = new(16, capacity * 2);
        }

        public readonly int Binding;
        public readonly int Capacity;
        public readonly nint[] Handles;
        public readonly TextureView[] TextureViews;
        public readonly Sampler[] Samplers;
        readonly WebGpuBindGroupCache Cache;
        public int TextureCount;
        public int HandleCount;
        public BindGroup BindGroup;
        public int Signature;
        public int BoundSignature;
        public bool BindGroupDirty;

        public ReadOnlySpan<nint> CurrentHandles
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Handles.AsSpan(0, HandleCount);
        }

        public bool TryGetCachedBindGroup(uint frameSerial, out BindGroup bindGroup)
            => Cache.TryGet(Signature, CurrentHandles, frameSerial, out bindGroup);

        public void CacheBindGroup(WebGpuGraphicsBackend backend, BindGroup bindGroup, uint frameSerial)
            => Cache.Store(backend, Signature, CurrentHandles, bindGroup, frameSerial);

        public void TrimCache(WebGpuGraphicsBackend backend, uint frameSerial)
            => Cache.Trim(backend, frameSerial);

        public void ClearCurrent(WebGpuGraphicsBackend backend)
        {
            Cache.Clear(backend);
            BindGroup = default;

            if (HandleCount != 0)
                Array.Clear(Handles, 0, HandleCount);

            Array.Clear(TextureViews);
            Array.Clear(Samplers);
            HandleCount = 0;
            TextureCount = 0;
            Signature = 0;
            BoundSignature = 0;
            BindGroupDirty = true;
        }

        public bool References(WebGpuResourceReference resource)
        {
            for (var i = 0; i < HandleCount; ++i)
                if (Handles[i] == resource.Handle)
                    return true;

            return false;
        }

        public bool PurgeCachedBindGroupsReferencing(WebGpuGraphicsBackend backend, WebGpuResourceReference resource)
        {
            var purged = References(resource);
            if (purged)
                BindGroup = default;

            return Cache.PurgeReferencing(backend, resource) || purged;
        }

        public void Dispose(WebGpuGraphicsBackend backend)
        {
            Cache?.Clear(backend);
            BindGroup = default;

            if (Handles is not null)
                Array.Clear(Handles);

            if (TextureViews is not null)
                Array.Clear(TextureViews);

            if (Samplers is not null)
                Array.Clear(Samplers);

            Cache?.Dispose();
        }
    }
}