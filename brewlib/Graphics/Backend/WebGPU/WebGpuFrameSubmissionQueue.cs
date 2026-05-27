namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Threading;
using Ahjo.Wgpu;

sealed class WebGpuFrameSubmissionQueue(WebGpuGraphicsBackend backend, WebGpuFrameExecutionMode mode) : IDisposable
{
    readonly SemaphoreSlim available = new(1, 1);
    readonly SemaphoreSlim queued = new(0);

    Exception exception;
    int hasPending, stop;
    PendingFrameSubmission pending;
    Thread thread;

    public bool HasPending => Volatile.Read(ref hasPending) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref stop, 1) != 0)
            return;

        queued.Release();

        var submitThread = Volatile.Read(ref thread);
        submitThread?.Join();

        thread = null;

        available.Dispose();
        queued.Dispose();
    }

    public bool TryClaimSubmissionSlot()
    {
        if (mode == WebGpuFrameExecutionMode.Synchronous)
            return Volatile.Read(ref stop) == 0;

        Start();

        available.Wait();

        if (Volatile.Read(ref stop) == 0)
            return true;

        available.Release();
        return false;
    }

    public void CancelClaimedSubmissionSlot()
    {
        if (mode != WebGpuFrameExecutionMode.Synchronous)
            available.Release();
    }

    public void EnqueueClaimed(PendingFrameSubmission submission)
    {
        if (mode == WebGpuFrameExecutionMode.Synchronous)
        {
            Submit(submission);
            return;
        }

        pending = submission;
        Volatile.Write(ref hasPending, 1);

        queued.Release();
    }

    public void SetPendingException(Exception pendingException)
        => Interlocked.CompareExchange(ref exception, pendingException, null);

    public void ThrowPendingException()
    {
        var pendingException = Interlocked.Exchange(ref exception, null);
        if (pendingException is not null)
            throw new InvalidOperationException("WebGPU frame submission failed", pendingException);
    }

    void Start()
    {
        if (Volatile.Read(ref thread) is not null)
            return;

        var newThread = new Thread(Run)
        {
            IsBackground = true,
            Name = "storybrew WebGPU frame submit"
        };

        if (Interlocked.CompareExchange(ref thread, newThread, null) is null)
            newThread.Start();
    }

    void Run()
    {
        while (true)
        {
            queued.Wait();

            if (Volatile.Read(ref stop) != 0 && Volatile.Read(ref hasPending) == 0)
                return;

            if (Volatile.Read(ref hasPending) == 0)
                continue;

            var submission = pending;
            pending = default;

            try
            {
                Submit(submission);
            }
            finally
            {
                Volatile.Write(ref hasPending, 0);
                available.Release();
            }
        }
    }

    void Submit(PendingFrameSubmission submission)
    {
        CommandBuffer commandBuffer = default;
        WebGpuSurfaceFrame surfaceFrame = default;

        try
        {
            commandBuffer = backend.EncodeRecordedFrame(submission.Frame, out surfaceFrame);
            backend.SubmitCommandBuffer(commandBuffer);
            commandBuffer = default;

            backend.VertexStagingBelt.Recall();

            if (surfaceFrame.ShouldPresent)
                backend.PresentSubmittedSurfaceTexture(surfaceFrame.Texture, surfaceFrame.TextureView);
            else
                backend.ReleaseSurfaceTexture(surfaceFrame.Texture, surfaceFrame.TextureView);
        }
        catch (Exception ex)
        {
            backend.MarkDeviceLost(ex);
            SetPendingException(ex);

            if (surfaceFrame.ShouldPresent)
                backend.CompleteSurfaceSubmission(surfaceFrame.Texture, surfaceFrame.TextureView);
            else
                backend.ReleaseSurfaceTexture(surfaceFrame.Texture, surfaceFrame.TextureView);
        }
        finally
        {
            if (!commandBuffer.IsNull && !backend.IsDeviceLost)
                commandBuffer.Dispose();
        }
    }

    public readonly struct PendingFrameSubmission(WebGpuRecordedFrame frame)
    {
        public readonly WebGpuRecordedFrame Frame = frame;
    }
}