namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using Silk.NET.WebGPU;
using Tiny.PooledCollections.Generic.Value;
using WgpuBuffer = Silk.NET.WebGPU.Buffer;
using WgpuRenderPipeline = Silk.NET.WebGPU.RenderPipeline;
using WgpuSampler = Silk.NET.WebGPU.Sampler;
using WgpuTexture = Silk.NET.WebGPU.Texture;
using WgpuTextureView = Silk.NET.WebGPU.TextureView;

unsafe sealed class WebGpuDeferredReleases
{
    static readonly PfnQueueWorkDoneCallback ReleaseFenceCallback = new((_, userdata) =>
    {
        var handle = GCHandle.FromIntPtr((nint)userdata);
        if (handle.Target is ReleaseFence fence)
            fence.Owner.onFenceCompleted(fence);
    });

    readonly ConcurrentQueue<ReleaseFence> activeFences = new();
    readonly ConcurrentQueue<ReleaseBatch> batchPool = new();
    readonly ConcurrentQueue<ReleaseFence> completedFences = new();
    readonly ConcurrentQueue<ReleaseFence> fencePool = new();

    readonly ConcurrentQueue<ReleaseItem> pendingItems = new();
    int activeFenceCount;

    public void Retire(BindGroup* bindGroup)
    {
        if (bindGroup is null) return;

        pendingItems.Enqueue(new(ReleaseItemKind.BindGroup, (nint)bindGroup));
    }

    public void Retire(WgpuBuffer* buffer)
    {
        if (buffer is null) return;

        pendingItems.Enqueue(new(ReleaseItemKind.Buffer, (nint)buffer));
    }

    public void Retire(WgpuRenderPipeline* renderPipeline)
    {
        if (renderPipeline is null) return;

        pendingItems.Enqueue(new(ReleaseItemKind.RenderPipeline, (nint)renderPipeline));
    }

    public void Retire(WgpuSampler* sampler)
    {
        if (sampler is null) return;

        pendingItems.Enqueue(new(ReleaseItemKind.Sampler, (nint)sampler));
    }

    public void Retire(WgpuTexture* texture)
    {
        if (texture is null) return;

        pendingItems.Enqueue(new(ReleaseItemKind.Texture, (nint)texture));
    }

    public void Retire(WgpuTextureView* textureView)
    {
        if (textureView is null) return;

        pendingItems.Enqueue(new(ReleaseItemKind.TextureView, (nint)textureView));
    }

    public void Retire(IDisposable disposable)
    {
        if (disposable is null) return;

        pendingItems.Enqueue(new(disposable));
    }

    public void NotifyQueueSubmitted(WebGPU wgpu, Queue* queue)
    {
        if (queue is null)
        {
            ReleaseCompleted(wgpu);
            return;
        }

        var batch = drainPendingItems();
        if (batch is not null)
            submitFence(wgpu, queue, batch);

        ReleaseCompleted(wgpu);
    }

    public void SubmitPendingWithEmptyQueueWork(WebGPU wgpu, Queue* queue)
    {
        if (queue is null || pendingItems.IsEmpty)
        {
            ReleaseCompleted(wgpu);
            return;
        }

        wgpu.QueueSubmit(queue, 0, null);
        NotifyQueueSubmitted(wgpu, queue);
    }

    public bool ReleaseCompleted(WebGPU wgpu)
    {
        var releasedAny = false;

        while (completedFences.TryDequeue(out var fence))
            releasedAny |= fence.TryReleaseCompleted(wgpu);

        pruneActiveFences();
        return releasedAny;
    }

    public void ReleaseAllNow(WebGPU wgpu)
    {
        // Shutdown path only: submission/rendering has stopped and no pass/encoder is open.
        // Do not wait for QueueOnSubmittedWorkDone because wgpu-native may not deliver callbacks
        // without implementation-specific polling. If a callback arrives later, it will only free
        // the GCHandle/fence shell; the batch has already been released.
        var leftover = drainPendingItems();
        if (leftover is not null)
            releaseAndReturn(wgpu, leftover);

        while (completedFences.TryDequeue(out var fence))
            fence.TryReleaseCompleted(wgpu);

        while (activeFences.TryDequeue(out var fence))
        {
            Interlocked.Decrement(ref activeFenceCount);
            fence.TryReleaseNow(wgpu);
            if (fence.CanReturnToPool)
                returnFence(fence);
        }
    }

    public void ClearWithoutRelease()
    {
        while (pendingItems.TryDequeue(out _)) { }

        while (completedFences.TryDequeue(out _)) { }

        while (activeFences.TryDequeue(out var fence))
        {
            Interlocked.Decrement(ref activeFenceCount);
            fence.DropWithoutRelease();
            if (fence.CanReturnToPool)
                returnFence(fence);
        }
    }

    void submitFence(WebGPU wgpu, Queue* queue, ReleaseBatch batch)
    {
        var fence = rentFence();
        fence.Reset(this, batch);
        activeFences.Enqueue(fence);
        Interlocked.Increment(ref activeFenceCount);

        wgpu.QueueOnSubmittedWorkDone(queue,
            ReleaseFenceCallback,
            (void*)GCHandle.ToIntPtr(fence.Handle));
    }

    void onFenceCompleted(ReleaseFence fence)
    {
        if (fence.MarkCompleted())
            completedFences.Enqueue(fence);
    }

    ReleaseBatch drainPendingItems()
    {
        ReleaseBatch batch = null;
        while (pendingItems.TryDequeue(out var item))
        {
            batch ??= rentBatch();
            batch.Add(item);
        }

        if (batch is null || !batch.IsEmpty) return batch;

        returnBatch(batch);
        return null;
    }

    ReleaseBatch rentBatch()
        => batchPool.TryDequeue(out var batch) ? batch : new();

    ReleaseFence rentFence()
        => fencePool.TryDequeue(out var fence) ? fence : new();

    void releaseAndReturn(WebGPU wgpu, ReleaseBatch batch)
    {
        try
        {
            batch.Release(wgpu);
        }
        finally
        {
            returnBatch(batch);
        }
    }

    void returnBatch(ReleaseBatch batch)
    {
        batch.Clear();
        batchPool.Enqueue(batch);
    }

    void returnFence(ReleaseFence fence)
    {
        fence.ClearForPool();
        fencePool.Enqueue(fence);
    }

    void pruneActiveFences()
    {
        var count = Volatile.Read(ref activeFenceCount);
        for (var i = 0; i < count; ++i)
        {
            if (!activeFences.TryDequeue(out var fence))
                return;

            if (fence.CanReturnToPool)
            {
                Interlocked.Decrement(ref activeFenceCount);
                returnFence(fence);
                continue;
            }

            activeFences.Enqueue(fence);
        }
    }

    sealed class ReleaseFence
    {
        const int Submitted = 0;
        const int CompletedQueued = 1;
        const int Released = 2;
        const int ReleasedAwaitingCallback = 3;
        ReleaseBatch batch;
        public GCHandle Handle;

        public WebGpuDeferredReleases Owner;

        int state;

        public bool CanReturnToPool => Volatile.Read(ref state) == Released;

        public void Reset(WebGpuDeferredReleases owner, ReleaseBatch releaseBatch)
        {
            Owner = owner;
            batch = releaseBatch;
            state = Submitted;
            Handle = GCHandle.Alloc(this);
        }

        public bool MarkCompleted()
        {
            if (Interlocked.CompareExchange(ref state, CompletedQueued, Submitted) == Submitted)
                return true;

            if (Interlocked.CompareExchange(ref state, Released, ReleasedAwaitingCallback) == ReleasedAwaitingCallback)
                freeHandle();

            return false;
        }

        public bool TryReleaseCompleted(WebGPU wgpu)
        {
            if (Interlocked.CompareExchange(ref state, Released, CompletedQueued) != CompletedQueued)
                return false;

            releaseBatch(wgpu);
            freeHandle();
            return true;
        }

        public void TryReleaseNow(WebGPU wgpu)
        {
            var previous = Interlocked.CompareExchange(ref state, ReleasedAwaitingCallback, Submitted);
            if (previous == Submitted)
            {
                releaseBatch(wgpu);
                return;
            }

            previous = Interlocked.CompareExchange(ref state, Released, CompletedQueued);
            if (previous == CompletedQueued)
            {
                releaseBatch(wgpu);
                freeHandle();
            }
        }

        public void DropWithoutRelease()
        {
            var previous = Interlocked.Exchange(ref state, ReleasedAwaitingCallback);
            if (previous is CompletedQueued or Released)
                freeHandle();

            batch = null;
        }

        public void ClearForPool()
        {
            Owner = null;
            batch = null;
            state = Submitted;
            Handle = default;
        }

        void releaseBatch(WebGPU wgpu)
        {
            var releaseBatch = Interlocked.Exchange(ref batch, null);
            if (releaseBatch is not null)
                Owner.releaseAndReturn(wgpu, releaseBatch);
        }

        void freeHandle()
        {
            if (Handle.IsAllocated)
                Handle.Free();
        }
    }

    sealed class ReleaseBatch
    {
        ValueList<WebGpuHandle<BindGroup>> bindGroups = ValueList.Create<WebGpuHandle<BindGroup>>();
        ValueList<WebGpuHandle<WgpuBuffer>> buffers = ValueList.Create<WebGpuHandle<WgpuBuffer>>();
        ValueList<IDisposable> disposables = ValueList.Create<IDisposable>();

        ValueList<WebGpuHandle<WgpuRenderPipeline>> renderPipelines =
            ValueList.Create<WebGpuHandle<WgpuRenderPipeline>>();

        ValueList<WebGpuHandle<WgpuSampler>> samplers = ValueList.Create<WebGpuHandle<WgpuSampler>>();
        ValueList<WebGpuHandle<WgpuTexture>> textures = ValueList.Create<WebGpuHandle<WgpuTexture>>();

        ValueList<WebGpuHandle<WgpuTextureView>> textureViews =
            ValueList.Create<WebGpuHandle<WgpuTextureView>>();

        public bool IsEmpty =>
            bindGroups.Count == 0 &&
            buffers.Count == 0 &&
            renderPipelines.Count == 0 &&
            samplers.Count == 0 &&
            textures.Count == 0 &&
            textureViews.Count == 0 &&
            disposables.Count == 0;

        public void Add(ReleaseItem item)
        {
            switch (item.Kind)
            {
                case ReleaseItemKind.BindGroup:
                    bindGroups.Add(new((BindGroup*)item.Handle));
                    break;

                case ReleaseItemKind.Buffer:
                    buffers.Add(new((WgpuBuffer*)item.Handle));
                    break;

                case ReleaseItemKind.RenderPipeline:
                    renderPipelines.Add(new((WgpuRenderPipeline*)item.Handle));
                    break;

                case ReleaseItemKind.Sampler:
                    samplers.Add(new((WgpuSampler*)item.Handle));
                    break;

                case ReleaseItemKind.Texture:
                    textures.Add(new((WgpuTexture*)item.Handle));
                    break;

                case ReleaseItemKind.TextureView:
                    textureViews.Add(new((WgpuTextureView*)item.Handle));
                    break;

                case ReleaseItemKind.Disposable:
                    disposables.Add(item.Disposable);
                    break;
            }
        }

        public void Release(WebGPU wgpu)
        {
            for (var i = 0; i < bindGroups.Count; ++i)
                wgpu.BindGroupRelease(bindGroups[i].Pointer);

            for (var i = 0; i < renderPipelines.Count; ++i)
                wgpu.RenderPipelineRelease(renderPipelines[i].Pointer);

            for (var i = 0; i < textureViews.Count; ++i)
                wgpu.TextureViewRelease(textureViews[i].Pointer);

            for (var i = 0; i < samplers.Count; ++i)
                wgpu.SamplerRelease(samplers[i].Pointer);

            for (var i = 0; i < textures.Count; ++i)
                wgpu.TextureRelease(textures[i].Pointer);

            for (var i = 0; i < buffers.Count; ++i)
                wgpu.BufferRelease(buffers[i].Pointer);

            for (var i = 0; i < disposables.Count; ++i)
                disposables[i].Dispose();
        }

        public void Clear()
        {
            bindGroups.Clear();
            buffers.Clear();
            renderPipelines.Clear();
            samplers.Clear();
            textures.Clear();
            textureViews.Clear();
            disposables.Clear();
        }
    }

    readonly struct ReleaseItem
    {
        public readonly ReleaseItemKind Kind;
        public readonly nint Handle;
        public readonly IDisposable Disposable;

        public ReleaseItem(ReleaseItemKind kind, nint handle)
        {
            Kind = kind;
            Handle = handle;
            Disposable = null;
        }

        public ReleaseItem(IDisposable disposable)
        {
            Kind = ReleaseItemKind.Disposable;
            Handle = 0;
            Disposable = disposable;
        }
    }

    enum ReleaseItemKind : byte
    {
        BindGroup,
        Buffer,
        RenderPipeline,
        Sampler,
        Texture,
        TextureView,
        Disposable
    }
}