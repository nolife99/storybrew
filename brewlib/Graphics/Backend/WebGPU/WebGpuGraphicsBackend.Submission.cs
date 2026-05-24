namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Threading;
using Silk.NET.WebGPU;

public unsafe sealed partial class WebGpuGraphicsBackend
{
    void submitFrameCommandEncoder(bool presentSurfaceTexture)
    {
        var submission = new PendingFrameSubmission(CommandEncoder,
            presentSurfaceTexture ? frameTexture : null,
            presentSurfaceTexture ? frameTextureView : null,
            presentSurfaceTexture,
            FrameSerial);

        CommandEncoder = null;
        frameTexture = null;
        frameTextureView = null;
        if (presentSurfaceTexture)
            lock (surfaceAcquireSync)
                surfaceSubmissionInFlight = true;

        startFrameSubmission();
        pendingFrameSubmissions.Enqueue(submission);
        frameSubmissionRequested.Release();
    }

    internal void SubmitCommandEncoder(CommandEncoder* encoder)
    {
        CommandBuffer* commandBuffer = null;
        var submitted = false;

        try
        {
            commandBuffer = Api.CommandEncoderFinish(encoder, null);
            if (commandBuffer is null)
                throw new InvalidOperationException("Unable to finish WebGPU command encoder");

            Api.QueueSubmit(QueueHandle, 1, &commandBuffer);
            submitted = true;
        }
        finally
        {
            if (commandBuffer is not null)
                Api.CommandBufferRelease(commandBuffer);

            if (encoder is not null)
                Api.CommandEncoderRelease(encoder);

            if (submitted)
                notifyDeferredReleasesAfterQueueSubmit();
        }
    }

    void startFrameSubmission()
    {
        if (frameSubmissionThread is not null)
            return;

        frameSubmissionThread = new(() =>
        {
            while (true)
            {
                frameSubmissionRequested.Wait();

                if (Interlocked.CompareExchange(ref frameSubmissionStop, true, true)) return;

                if (!pendingFrameSubmissions.TryDequeue(out var submission))
                    continue;

                submitFrameSubmission(submission);
            }
        })
        {
            IsBackground = true,
            Name = "storybrew WebGPU frame submit"
        };

        frameSubmissionThread.Start();
    }

    void submitFrameSubmission(PendingFrameSubmission submission)
    {
        try
        {
            SubmitCommandEncoder(submission.CommandEncoder);
            if (submission.PresentSurfaceTexture)
                presentSubmittedSurfaceTexture(submission.SurfaceTexture, submission.SurfaceTextureView);
            else
                releaseSurfaceTexture(submission.SurfaceTexture, submission.SurfaceTextureView);
        }
        catch (Exception ex)
        {
            Interlocked.CompareExchange(ref frameSubmissionException, ex, null);
            if (submission.PresentSurfaceTexture)
                completeSurfaceSubmission(submission.SurfaceTexture, submission.SurfaceTextureView);
            else
                releaseSurfaceTexture(submission.SurfaceTexture, submission.SurfaceTextureView);
        }
    }

    void throwPendingFrameSubmissionException()
    {
        var exception = Interlocked.Exchange(ref frameSubmissionException, null);
        if (exception is not null)
            throw new InvalidOperationException("WebGPU frame submission failed", exception);
    }

    void stopFrameSubmission()
    {
        Interlocked.Exchange(ref frameSubmissionStop, true);

        frameSubmissionRequested.Release();
        frameSubmissionThread?.Join();
        frameSubmissionThread = null;
    }
}