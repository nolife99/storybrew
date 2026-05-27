namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Threading;
using Ahjo.Wgpu;

sealed class WebGpuFrameSubmissionQueue(WebGpuGraphicsBackend backend, WebGpuFrameExecutionMode mode) : IDisposable
{
    readonly AutoResetEvent available = new(true);
    readonly AutoResetEvent queued = new(false);
    Exception exception;
    int hasPending, stop;
    PendingFrameSubmission pending;
    Thread thread;

    public bool HasPending => Volatile.Read(ref hasPending) != 0;

    public void Dispose()
    {
        Interlocked.Exchange(ref stop, 1);
        queued.Set();
        thread?.Join();
        thread = null;
        available.Dispose();
        queued.Dispose();
    }

    public void Enqueue(PendingFrameSubmission submission)
    {
        if (!TryClaimSubmissionSlot()) return;

        EnqueueClaimed(submission);
    }

    public bool TryClaimSubmissionSlot()
    {
        if (mode == WebGpuFrameExecutionMode.Synchronous)
            return Volatile.Read(ref stop) == 0;

        Start();
        available.WaitOne();
        if (Volatile.Read(ref stop) == 0) return true;

        available.Set();
        return false;
    }

    public void CancelClaimedSubmissionSlot()
    {
        if (mode != WebGpuFrameExecutionMode.Synchronous)
            available.Set();
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
        queued.Set();
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
        if (thread is not null)
            return;

        thread = new(Run)
        {
            IsBackground = true,
            Name = "storybrew WebGPU frame submit"
        };

        thread.Start();
    }

    void Run()
    {
        while (true)
        {
            queued.WaitOne();

            if (Volatile.Read(ref stop) != 0 && Volatile.Read(ref hasPending) == 0)
                return;

            if (Volatile.Read(ref hasPending) == 0)
                continue;

            var submission = pending;
            pending = default;
            Submit(submission);
            Volatile.Write(ref hasPending, 0);
            available.Set();
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

            // Recall is called here — submission thread only — keeping all vertex belt
            // operations (Poll/WriteBuffer/Finish/Recall) on this single thread.
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