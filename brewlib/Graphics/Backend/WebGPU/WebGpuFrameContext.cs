namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Generic;
using Ahjo.Wgpu;

/// <summary>
///     Tracks frame liveness and native resources that may still be referenced by in-flight command buffers.
///     Disposal is fenced by the queue's submitted-work completion request, not by an arbitrary frame count. This
///     matters for debug/validation builds and for slow frames: a resource recorded into frame N must not be released
///     until the queue reports that frame N's submission has completed.
/// </summary>
sealed class WebGpuFrameContext : IDisposable
{
    readonly WebGpuDeviceContext deviceContext;
    readonly List<FencedBatch> fenced = new(8);
    readonly List<IDisposable> pending = new(32);
    bool disposed;

    public WebGpuFrameContext(WebGpuDeviceContext deviceContext)
        => this.deviceContext = deviceContext ?? throw new ArgumentNullException(nameof(deviceContext));

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
    }

    public void BeginFrame()
    {
        if (IsFrameActive)
            throw new InvalidOperationException("BeginFrame called while frame already active");

        ReleaseCompleted();
        IsFrameActive = true;
    }

    public void EndFrame()
    {
        if (!IsFrameActive)
            throw new InvalidOperationException("EndFrame called without an active frame");

        IsFrameActive = false;
        FencePending();
    }

    public void EnqueueDeferred(IDisposable resource)
    {
        if (resource is null) return;

        pending.Add(resource);

        if (!IsFrameActive)
            FencePending();
    }

    void FencePending()
    {
        if (pending.Count == 0) return;

        var items = pending.ToArray();
        pending.Clear();

        fenced.Add(new(deviceContext.Queue.BeginOnSubmittedWorkDone(), items));
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

    sealed class FencedBatch : IDisposable
    {
        IDisposable[] items;
        public QueueWorkDoneRequest Request;

        public FencedBatch(QueueWorkDoneRequest request, IDisposable[] items)
        {
            Request = request;
            this.items = items;
        }

        public void Dispose()
        {
            if (items is not null)
            {
                foreach (var item in items)
                    item.Dispose();

                items = null;
            }

            Request.Dispose();
            Request = default;
        }
    }
}