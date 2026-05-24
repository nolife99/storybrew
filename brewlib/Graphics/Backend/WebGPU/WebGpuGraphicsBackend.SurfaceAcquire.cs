namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Silk.NET.WebGPU;

public unsafe sealed partial class WebGpuGraphicsBackend
{
    readonly ManualResetEventSlim surfaceAcquireReady = new(false);
    readonly SemaphoreSlim surfaceAcquireRequested = new(0);
    readonly Lock surfaceAcquireSync = new();

    bool tryPrepareFrameSurfaceTexture(bool waitForAcquire = false)
    {
        while (true)
        {
            if (frameTextureView is not null)
                return true;

            Exception exception = null;
            SurfaceGetCurrentTextureStatus status = default;
            var hasResult = false;
            var shouldRequest = false;
            var shouldWait = false;

            lock (surfaceAcquireSync)
            {
                if (preparedSurfaceTextureView is not null)
                {
                    frameTexture = preparedSurfaceTexture;
                    frameTextureView = preparedSurfaceTextureView;
                    preparedSurfaceTexture = null;
                    preparedSurfaceTextureView = null;
                    surfaceAcquireHasResult = false;
                    surfaceAcquireException = null;
                    return true;
                }

                if (surfaceAcquireHasResult)
                {
                    hasResult = true;
                    status = preparedSurfaceStatus;
                    exception = surfaceAcquireException;
                    surfaceAcquireHasResult = false;
                    surfaceAcquireException = null;
                }
                else
                {
                    shouldRequest = !surfaceAcquireInFlight;
                    shouldWait = surfaceAcquireInFlight;
                }
            }

            if (exception is not null)
                ExceptionDispatchInfo.Capture(exception).Throw();

            if (hasResult)
            {
                if (status is SurfaceGetCurrentTextureStatus.Outdated or SurfaceGetCurrentTextureStatus.Lost)
                {
                    surfaceConfigured = false;
                    configureSurface(true);
                }

                requestSurfaceTextureAcquire();
                if (!waitForAcquire || status is SurfaceGetCurrentTextureStatus.Timeout)
                    return false;

                waitForSurfaceTextureAcquire();
                continue;
            }

            if (shouldRequest)
            {
                requestSurfaceTextureAcquire();
                shouldWait = true;
            }

            if (!waitForAcquire)
                return false;

            if (!shouldWait)
                return false;

            waitForSurfaceTextureAcquire();
        }
    }

    void requestSurfaceTextureAcquire()
    {
        if (disposed ||
            surface is null ||
            FramebufferWidth == 0 ||
            FramebufferHeight == 0)
            return;

        startSurfaceTextureAcquire();

        lock (surfaceAcquireSync)
        {
            if (surfaceAcquireStop ||
                surfaceSubmissionInFlight ||
                surfaceAcquireInFlight ||
                surfaceAcquireHasResult ||
                preparedSurfaceTexture is not null)
                return;

            surfaceAcquireInFlight = true;
            surfaceAcquireReady.Reset();
        }

        surfaceAcquireRequested.Release();
    }

    void startSurfaceTextureAcquire()
    {
        if (surfaceAcquireThread is not null)
            return;

        surfaceAcquireThread = new(surfaceTextureAcquireLoop)
        {
            IsBackground = true,
            Name = "storybrew WebGPU surface acquire"
        };

        surfaceAcquireThread.Start();
    }

    void surfaceTextureAcquireLoop()
    {
        while (true)
        {
            surfaceAcquireRequested.Wait();

            lock (surfaceAcquireSync)
            {
                if (surfaceAcquireStop)
                    return;
            }

            Texture* texture = null;
            TextureView* textureView = null;
            Exception exception = null;
            var status = SurfaceGetCurrentTextureStatus.Success;

            try
            {
                status = tryAcquireSurfaceTexture(out texture);
                if (texture is not null)
                    textureView = createFrameTextureView(texture);
            }
            catch (Exception ex)
            {
                if (textureView is not null)
                    Api.TextureViewRelease(textureView);

                if (texture is not null)
                    Api.TextureRelease(texture);

                texture = null;
                textureView = null;
                exception = ex;
            }

            lock (surfaceAcquireSync)
            {
                surfaceAcquireInFlight = false;
                surfaceAcquireHasResult = true;
                preparedSurfaceStatus = status;
                surfaceAcquireException = exception;
                preparedSurfaceTexture = texture;
                preparedSurfaceTextureView = textureView;
            }

            surfaceAcquireReady.Set();
        }
    }

    SurfaceGetCurrentTextureStatus tryAcquireSurfaceTexture(out Texture* texture)
    {
        SurfaceTexture surfaceTexture = default;
        Api.SurfaceGetCurrentTexture(surface, ref surfaceTexture);

        texture = null;
        switch (surfaceTexture.Status)
        {
            case SurfaceGetCurrentTextureStatus.Success:
                texture = surfaceTexture.Texture;
                return surfaceTexture.Status;

            case SurfaceGetCurrentTextureStatus.Timeout:
            case SurfaceGetCurrentTextureStatus.Outdated:
            case SurfaceGetCurrentTextureStatus.Lost:
                return surfaceTexture.Status;

            case SurfaceGetCurrentTextureStatus.OutOfMemory:
            case SurfaceGetCurrentTextureStatus.DeviceLost:
                throw new InvalidOperationException($"Unable to acquire WebGPU surface texture: {surfaceTexture.Status}");

            default:
                throw new InvalidOperationException($"Unexpected WebGPU surface texture status: {surfaceTexture.Status}");
        }
    }

    TextureView* createFrameTextureView(Texture* texture)
    {
        TextureViewDescriptor descriptor = new()
        {
            Format = SurfaceFormat,
            Dimension = TextureViewDimension.Dimension2D,
            MipLevelCount = 1,
            ArrayLayerCount = 1,
            Aspect = TextureAspect.All
        };

        var view = Api.TextureCreateView(texture, in descriptor);
        return view is not null ? view : throw new InvalidOperationException("Unable to create WebGPU frame texture view");
    }

    void presentSubmittedSurfaceTexture(Texture* texture, TextureView* textureView)
    {
        if (texture is null) return;

        Api.SurfacePresent(surface);
        releaseSurfaceTexture(texture, textureView);
        lock (surfaceAcquireSync)
            surfaceSubmissionInFlight = false;

        requestSurfaceTextureAcquire();
    }

    void releaseFrameSurfaceTexture()
    {
        releaseSurfaceTexture(frameTexture, frameTextureView);
        frameTexture = null;
        frameTextureView = null;
    }

    void releaseSurfaceTexture(Texture* texture, TextureView* textureView)
    {
        if (textureView is not null)
            Api.TextureViewRelease(textureView);

        if (texture is not null)
            Api.TextureRelease(texture);
    }

    void completeSurfaceSubmission(Texture* texture, TextureView* textureView)
    {
        releaseSurfaceTexture(texture, textureView);
        lock (surfaceAcquireSync)
            surfaceSubmissionInFlight = false;
    }

    void releasePreparedSurfaceTexture()
    {
        Texture* texture;
        TextureView* textureView;
        lock (surfaceAcquireSync)
        {
            texture = preparedSurfaceTexture;
            textureView = preparedSurfaceTextureView;
            preparedSurfaceTexture = null;
            preparedSurfaceTextureView = null;
            surfaceAcquireHasResult = false;
            surfaceAcquireException = null;
        }

        if (textureView is not null)
            Api.TextureViewRelease(textureView);

        if (texture is not null)
            Api.TextureRelease(texture);
    }

    void waitForSurfaceTextureAcquire()
    {
        while (true)
        {
            lock (surfaceAcquireSync)
            {
                if (!surfaceAcquireInFlight)
                    return;
            }

            surfaceAcquireReady.Wait();
        }
    }

    void stopSurfaceTextureAcquire()
    {
        lock (surfaceAcquireSync)
            surfaceAcquireStop = true;

        surfaceAcquireRequested.Release();

        surfaceAcquireThread?.Join();
        surfaceAcquireThread = null;
    }

    readonly struct PendingFrameSubmission(
        CommandEncoder* commandEncoder,
        Texture* surfaceTexture,
        TextureView* surfaceTextureView,
        bool presentSurfaceTexture,
        uint frameSerial)
    {
        public readonly CommandEncoder* CommandEncoder = commandEncoder;
        public readonly Texture* SurfaceTexture = surfaceTexture;
        public readonly TextureView* SurfaceTextureView = surfaceTextureView;
        public readonly bool PresentSurfaceTexture = presentSurfaceTexture;
        public readonly uint FrameSerial = frameSerial;
    }
}