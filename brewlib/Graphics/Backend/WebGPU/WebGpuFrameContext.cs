namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Util;
using WgpuBuffer = Ahjo.Wgpu.Buffer;

/// <summary>
///     Tracks frame liveness and native resources that may still be referenced by in-flight command buffers.
///     Disposal is fenced by the queue's submitted-work completion request, not by an arbitrary frame count. This
///     matters for debug/validation builds and for slow frames: a resource recorded into frame N must not be released
///     until the queue reports that frame N's submission has completed.
/// </summary>
sealed class WebGpuFrameContext : IDisposable
{
    const ulong UploadChunkSize = 2 * 1024 * 1024;

    readonly WebGpuDeviceContext deviceContext;
    readonly List<FencedBatch> fenced = new(8);
    readonly List<DeferredResource> pending = new(32);
    readonly List<StagedBufferWrite> stagedBufferWrites = new(128);
    readonly Queue<QueueWorkDoneRequest> uploadRecallRequests = new();
    readonly StagingBelt uploadBelt;

    byte[] uploadArena;
    bool disposed;
    int uploadArenaLength;
    bool uploadBeltFinished;

    public WebGpuFrameContext(WebGpuDeviceContext deviceContext)
    {
        this.deviceContext = deviceContext ?? throw new ArgumentNullException(nameof(deviceContext));
        uploadBelt = new(deviceContext.Device, UploadChunkSize);
    }

    public bool IsFrameActive { get; private set; }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;

        foreach (var resource in pending)
            resource.Dispose();

        pending.Clear();

        foreach (var batch in fenced)
            batch.Dispose();

        fenced.Clear();

        WaitForUploadRecall();
        while (uploadRecallRequests.Count != 0)
            uploadRecallRequests.Dequeue().Dispose();

        uploadBelt.Dispose();
    }

    public void BeginFrame()
    {
        if (IsFrameActive)
            throw new InvalidOperationException("BeginFrame called while frame already active");

        ReleaseCompleted();
        RecallCompletedUploads();
        IsFrameActive = true;
        stagedBufferWrites.Clear();
        uploadArenaLength = 0;
        uploadBeltFinished = false;
    }

    public void EndFrame()
    {
        if (!IsFrameActive)
            throw new InvalidOperationException("EndFrame called without an active frame");

        IsFrameActive = false;

        if (uploadBeltFinished)
        {
            uploadRecallRequests.Enqueue(deviceContext.Queue.BeginOnSubmittedWorkDone());
            uploadBeltFinished = false;
        }

        stagedBufferWrites.Clear();
        uploadArenaLength = 0;
        FencePending();
    }

    public void AbortFrame()
    {
        if (!IsFrameActive)
            return;

        IsFrameActive = false;
        stagedBufferWrites.Clear();
        uploadArenaLength = 0;
        uploadBeltFinished = false;
        FencePending();
    }

    public void StageBufferWrite(WgpuBuffer buffer, ulong offset, scoped ReadOnlySpan<byte> data)
        => StageBufferWrite(buffer, offset, data, data.Length);

    public void StageBufferWrite(WgpuBuffer buffer, ulong offset, scoped ReadOnlySpan<byte> data, int paddedLength)
    {
        if (buffer.IsNull || data.IsEmpty) return;
        if (paddedLength < data.Length)
            throw new ArgumentOutOfRangeException(nameof(paddedLength), paddedLength, null);

        var last = stagedBufferWrites.Count - 1;
        if (last >= 0)
        {
            var write = stagedBufferWrites[last];
            if (write.CanAppend(buffer, offset, uploadArenaLength))
            {
                AppendUploadBytes(data, paddedLength);
                write.Length += paddedLength;
                stagedBufferWrites[last] = write;
                return;
            }
        }

        var arenaOffset = uploadArenaLength;
        AppendUploadBytes(data, paddedLength);
        stagedBufferWrites.Add(new(buffer, offset, arenaOffset, paddedLength));
    }

    public bool FlushUploads(CommandEncoder encoder)
    {
        if (stagedBufferWrites.Count == 0)
            return false;

        foreach (var write in stagedBufferWrites)
        {
            var mapped = uploadBelt.WriteBuffer(encoder, write.Buffer, write.Offset, (ulong)write.Length);
            uploadArena.AsSpan(write.ArenaOffset, write.Length).CopyTo(mapped);
        }

        return true;
    }

    public void FinishUploads()
    {
        if (stagedBufferWrites.Count == 0)
            return;

        uploadBelt.Finish();
        uploadBeltFinished = true;
    }

    public void EnqueueDeferred(WgpuBuffer buffer)
    {
        if (buffer.IsNull) return;

        pending.Add(DeferredResource.Buffer(buffer));

        if (!IsFrameActive)
            FencePending();
    }

    public void EnqueueDeferred(BindGroup bindGroup)
    {
        if (bindGroup.IsNull) return;

        pending.Add(DeferredResource.BindGroup(bindGroup));

        if (!IsFrameActive)
            FencePending();
    }

    void AppendUploadBytes(scoped ReadOnlySpan<byte> data, int paddedLength)
    {
        EnsureUploadArenaCapacity(checked(uploadArenaLength + paddedLength));
        data.CopyTo(uploadArena.AsSpan(uploadArenaLength));

        var padding = paddedLength - data.Length;
        if (padding != 0)
            uploadArena.AsSpan(uploadArenaLength + data.Length, padding).Clear();

        uploadArenaLength += paddedLength;
    }

    void EnsureUploadArenaCapacity(int required)
    {
        if (uploadArena is not null && uploadArena.Length >= required) return;

        var newSize = uploadArena is null ? 256 * 1024 : uploadArena.Length;
        while (newSize < required)
            newSize = checked(newSize * 2);

        var replacement = GC.AllocateUninitializedArray<byte>(newSize);
        if (uploadArenaLength != 0)
            uploadArena.AsSpan(0, uploadArenaLength).CopyTo(replacement);

        uploadArena = replacement;
    }

    void RecallCompletedUploads()
    {
        while (uploadRecallRequests.Count != 0)
        {
            var request = uploadRecallRequests.Peek();
            if (!request.IsComplete) return;

            uploadRecallRequests.Dequeue();
            request.Dispose();
            uploadBelt.Recall();
        }
    }

    void WaitForUploadRecall()
    {
        while (uploadRecallRequests.Count != 0)
        {
            deviceContext.Device.ProcessEvents();
            RecallCompletedUploads();
            if (uploadRecallRequests.Count != 0)
                Thread.Yield();
        }
    }

    void FencePending()
    {
        if (pending.Count == 0) return;

        var count = pending.Count;
        var items = ArrayPool<DeferredResource>.Shared.Rent(count);
        for (var i = 0; i < count; ++i)
            items[i] = pending[i];

        pending.Clear();

        fenced.Add(new(deviceContext.Queue.BeginOnSubmittedWorkDone(), items, count));
    }

    void ReleaseCompleted()
    {
        for (var i = fenced.Count - 1; i >= 0; --i)
        {
            var batch = fenced[i];
            if (!batch.Request.IsComplete)
                continue;

            batch.Dispose();
            fenced[i] = fenced[^1];
            fenced.RemoveAt(fenced.Count - 1);
        }
    }

    struct StagedBufferWrite
    {
        public readonly WgpuBuffer Buffer;
        public readonly ulong Offset;
        public readonly int ArenaOffset;
        public int Length;

        public StagedBufferWrite(WgpuBuffer buffer, ulong offset, int arenaOffset, int length)
        {
            Buffer = buffer;
            Offset = offset;
            ArenaOffset = arenaOffset;
            Length = length;
        }

        public bool CanAppend(WgpuBuffer buffer, ulong offset, int arenaLength)
            => Buffer.Equals(buffer) &&
               ArenaOffset + Length == arenaLength &&
               Offset + (ulong)Length == offset;
    }

    readonly struct DeferredResource
    {
        readonly byte kind;
        readonly WgpuBuffer buffer;
        readonly BindGroup bindGroup;

        DeferredResource(byte kind, WgpuBuffer buffer, BindGroup bindGroup)
        {
            this.kind = kind;
            this.buffer = buffer;
            this.bindGroup = bindGroup;
        }

        public static DeferredResource Buffer(WgpuBuffer buffer) => new(1, buffer, default);
        public static DeferredResource BindGroup(BindGroup bindGroup) => new(2, default, bindGroup);

        public void Dispose()
        {
            switch (kind)
            {
                case 1:
                    if (!buffer.IsNull) buffer.Dispose();
                    break;

                case 2:
                    if (!bindGroup.IsNull) bindGroup.Dispose();
                    break;
            }
        }
    }

    struct FencedBatch : IDisposable
    {
        DeferredResource[] items;
        int count;
        public QueueWorkDoneRequest Request;

        public FencedBatch(QueueWorkDoneRequest request, DeferredResource[] items, int count)
        {
            Request = request;
            this.items = items;
            this.count = count;
        }

        public void Dispose()
        {
            if (items is not null)
            {
                for (var i = 0; i < count; ++i)
                {
                    items[i].Dispose();
                    items[i] = default;
                }

                ArrayPool<DeferredResource>.Shared.Return(items);
                items = null;
                count = 0;
            }

            Request.Dispose();
            Request = default;
        }
    }
}