namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Threading;
using Ahjo.Wgpu;
using WgpuBuffer = Ahjo.Wgpu.Buffer;

sealed class WebGpuDeferredReleaseManager(WebGpuGraphicsBackend backend, Lock queueSync, Func<bool> hasPendingFrameSubmissions)
    : IDisposable
{
    readonly WebGpuDeferredReleases releases = new();
    readonly ManualResetEventSlim workerIdle = new(true);
    readonly DeferredReleaseWorkItem workItem = new(backend);
    bool disposed;

    int maintenanceRequested;
    int submitPendingRequested;
    int workerRunning;

    public void Dispose()
    {
        disposed = true;
        workerIdle.Dispose();
    }

    public void Retire(BindGroup bindGroup)
    {
        if (!backend.IsDeviceLost && !bindGroup.IsNull) releases.Retire(bindGroup);
    }

    public void Retire(WgpuBuffer buffer)
    {
        if (!backend.IsDeviceLost && !buffer.IsNull) releases.Retire(buffer);
    }

    public void Retire(RenderPipeline renderPipeline)
    {
        if (!backend.IsDeviceLost && !renderPipeline.IsNull) releases.Retire(renderPipeline);
    }

    public void Retire(Sampler sampler)
    {
        if (!backend.IsDeviceLost && !sampler.IsNull) releases.Retire(sampler);
    }

    public void Retire(Texture texture)
    {
        if (!backend.IsDeviceLost && !texture.IsNull) releases.Retire(texture);
    }

    public void Retire(TextureView textureView)
    {
        if (!backend.IsDeviceLost && !textureView.IsNull) releases.Retire(textureView);
    }

    public void Retire(IDisposable disposable)
    {
        if (!backend.IsDeviceLost)
            releases.Retire(disposable);
    }

    public void NotifyQueueSubmitted()
    {
        var queue = backend.QueueHandle;
        if (queue is null || backend.IsDeviceLost) return;

        try
        {
            releases.NotifyQueueSubmitted(queue);
            Schedule();
        }
        catch (Exception ex)
        {
            backend.MarkDeviceLost(ex);
            throw;
        }
    }

    public void Schedule(bool submitPending = false)
    {
        if (backend.IsDeviceLost)
        {
            releases.ClearWithoutRelease();
            return;
        }

        if (submitPending)
            Interlocked.Exchange(ref submitPendingRequested, 1);

        Interlocked.Exchange(ref maintenanceRequested, 1);
        if (Interlocked.CompareExchange(ref workerRunning, 1, 0) != 0)
            return;

        workerIdle.Reset();
        ThreadPool.UnsafeQueueUserWorkItem(workItem, false);
    }

    public void RunMaintenance()
    {
        try
        {
            while (!disposed)
            {
                var submitPending = Interlocked.Exchange(ref submitPendingRequested, 0) != 0;
                Interlocked.Exchange(ref maintenanceRequested, 0);

                if (backend.IsDeviceLost)
                {
                    releases.ClearWithoutRelease();
                    return;
                }

                var queue = backend.QueueHandle;
                var device = backend.DeviceHandle;
                if (queue is null || device is null)
                {
                    releases.ClearWithoutRelease();
                    return;
                }

                if (submitPending && !hasPendingFrameSubmissions())
                {
                    lock (queueSync)
                        releases.SubmitPendingWithEmptyQueueWork(queue);
                }

                device.ProcessEvents();
                backend.PollStagingBelt();
                releases.ReleaseCompleted();

                if (Volatile.Read(ref maintenanceRequested) == 0 &&
                    Volatile.Read(ref submitPendingRequested) == 0)
                    return;
            }
        }
        catch (Exception ex)
        {
            backend.MarkDeviceLost(ex);
        }
        finally
        {
            Interlocked.Exchange(ref workerRunning, 0);
            workerIdle.Set();

            if (!disposed &&
                !backend.IsDeviceLost &&
                (Volatile.Read(ref maintenanceRequested) != 0 ||
                    Volatile.Read(ref submitPendingRequested) != 0))
                Schedule();
        }
    }

    public void WaitForAll()
    {
        workerIdle.Wait();

        if (backend.DeviceHandle is null || backend.IsDeviceLost)
        {
            releases.ClearWithoutRelease();
            return;
        }

        try
        {
            var queue = backend.QueueHandle;
            if (queue is not null)
                lock (queueSync)
                    releases.SubmitPendingWithEmptyQueueWork(queue);

            backend.DeviceHandle.ProcessEvents();
            backend.PollStagingBelt();
            releases.ReleaseAllNow();
        }
        catch (Exception ex)
        {
            backend.MarkDeviceLost(ex);
        }
    }

    public void ClearWithoutRelease()
        => releases.ClearWithoutRelease();

    sealed class DeferredReleaseWorkItem(WebGpuGraphicsBackend backend) : IThreadPoolWorkItem
    {
        public void Execute()
            => backend.DeferredReleases.RunMaintenance();
    }
}