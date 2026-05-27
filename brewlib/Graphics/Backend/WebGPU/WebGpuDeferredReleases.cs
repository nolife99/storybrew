namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Concurrent;
using Ahjo.Wgpu;

sealed class WebGpuDeferredReleases
{
    readonly ConcurrentQueue<ReleaseFence> activeFences = new();
    readonly ConcurrentQueue<ReleaseBatch> batchPool = new();
    readonly ConcurrentQueue<ReleaseFence> fencePool = new();
    readonly ConcurrentQueue<IDisposable> pendingItems = new();

    public void Retire<T>(T resource) where T : struct, IDisposable
    {
        pendingItems.Enqueue(resource);
    }

    public void Retire(IDisposable disposable)
    {
        if (disposable is null) return;

        pendingItems.Enqueue(disposable);
    }

    public void NotifyQueueSubmitted(Queue queue)
    {
        if (queue is null)
        {
            ReleaseCompleted();
            return;
        }

        var batch = drainPendingItems();
        if (batch is not null)
        {
            var fence = rentFence();
            fence.Reset(queue.BeginOnSubmittedWorkDone(), batch);
            activeFences.Enqueue(fence);
        }

        ReleaseCompleted();
    }

    public void SubmitPendingWithEmptyQueueWork(Queue queue)
    {
        if (queue is null || pendingItems.IsEmpty)
        {
            ReleaseCompleted();
            return;
        }

        queue.Submit(ReadOnlySpan<CommandBuffer>.Empty);
        NotifyQueueSubmitted(queue);
    }

    public bool ReleaseCompleted()
    {
        var releasedAny = false;
        var count = activeFences.Count;
        for (var i = 0; i < count; ++i)
        {
            if (!activeFences.TryDequeue(out var fence)) break;

            if (fence.IsComplete)
            {
                fence.Release();
                releasedAny = true;
                returnFence(fence);
            }
            else
                activeFences.Enqueue(fence);
        }

        return releasedAny;
    }

    public void ReleaseAllNow()
    {
        var leftover = drainPendingItems();
        leftover?.Release();
        if (leftover is not null)
            returnBatch(leftover);

        while (activeFences.TryDequeue(out var fence))
        {
            fence.Release();
            returnFence(fence);
        }
    }

    public void ClearWithoutRelease()
    {
        while (pendingItems.TryDequeue(out _)) { }

        while (activeFences.TryDequeue(out var fence))
        {
            fence.DropWithoutRelease();
            returnFence(fence);
        }
    }

    ReleaseBatch drainPendingItems()
    {
        ReleaseBatch batch = null;
        while (pendingItems.TryDequeue(out var item))
        {
            batch ??= rentBatch();
            batch.Add(item);
        }

        return batch;
    }

    ReleaseBatch rentBatch()
        => batchPool.TryDequeue(out var batch) ? batch : new();

    ReleaseFence rentFence()
        => fencePool.TryDequeue(out var fence) ? fence : new();

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

    sealed class ReleaseFence
    {
        ReleaseBatch batch;
        bool hasRequest;
        QueueWorkDoneRequest request;

        public bool IsComplete => !hasRequest || request.IsComplete;

        public void Reset(QueueWorkDoneRequest workDoneRequest, ReleaseBatch releaseBatch)
        {
            request = workDoneRequest;
            batch = releaseBatch;
            hasRequest = true;
        }

        public void Release()
        {
            try
            {
                batch?.Release();
                if (hasRequest)
                    request.Dispose();
            }
            finally
            {
                batch = null;
                request = default;
                hasRequest = false;
            }
        }

        public void DropWithoutRelease()
        {
            if (hasRequest)
                request.Dispose();

            batch = null;
            request = default;
            hasRequest = false;
        }

        public void ClearForPool()
        {
            batch = null;
            request = default;
            hasRequest = false;
        }
    }

    sealed class ReleaseBatch
    {
        readonly ConcurrentQueue<IDisposable> items = new();

        public void Add(IDisposable item)
            => items.Enqueue(item);

        public void Release()
        {
            while (items.TryDequeue(out var item))
                item.Dispose();
        }

        public void Clear()
        {
            while (items.TryDequeue(out _)) { }
        }
    }
}