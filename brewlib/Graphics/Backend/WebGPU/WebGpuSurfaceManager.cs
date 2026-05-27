namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using SDL3;
using Surface = Ahjo.Wgpu.Surface;

unsafe sealed class WebGpuSurfaceManager : IDisposable
{
    readonly ManualResetEventSlim acquireReady = new(false);
    readonly SemaphoreSlim acquireRequested = new(0);
    readonly WebGpuGraphicsBackend backend;
    readonly Instance instance;
    readonly Lock sync = new();
    Exception acquireException;
    bool acquireInFlight, acquireHasResult, acquireStop, submissionInFlight, configured;
    Thread acquireThread;

    SurfaceCapabilities capabilities;
    WGPUCompositeAlphaMode configuredAlphaMode;
    WGPUTextureFormat configuredFormat;
    WGPUPresentMode configuredPresentMode;
    uint configuredWidth, configuredHeight;
    uint desiredMaximumFrameLatency;

    Texture frameTexture;
    TextureView frameTextureView;
    nint metalView;

    WGPUSurfaceGetCurrentTextureStatus preparedStatus;
    Texture preparedTexture;
    TextureView preparedTextureView;

    public WebGpuSurfaceManager(WebGpuGraphicsBackend backend, Instance instance, nint window)
    {
        this.backend = backend;
        this.instance = instance;
        Surface = WebGpuSurfaceFactory.Create(instance, window, out metalView);
    }

    public Surface Surface { get; private set; }
    public WGPUTextureFormat Format { get; private set; }
    public uint Width { get; private set; }
    public uint Height { get; private set; }
    public TextureView FrameTextureView => frameTextureView;
    public bool HasSurface => !Surface.IsNull;
    public bool IsFormatSrgb => Format is WGPUTextureFormat.BGRA8UnormSrgb or WGPUTextureFormat.RGBA8UnormSrgb;

    public void Dispose()
    {
        lock (sync)
            acquireStop = true;

        acquireRequested.Release();
        acquireThread?.Join();
        acquireThread = null;

        ReleasePreparedTexture();
        ReleaseFrameTexture();

        acquireRequested.Dispose();
        acquireReady.Dispose();

        if (!Surface.IsNull)
        {
            configured = false;
            Surface.Dispose();
            Surface = default;
        }

        if (metalView != 0)
        {
            SDL.MetalDestroyView(metalView);
            metalView = 0;
        }
    }

    public void Initialize(Adapter adapter)
    {
        capabilities = Surface.GetCapabilities(adapter);
        if (capabilities.Formats.Length == 0)
            throw new InvalidOperationException("WebGPU surface reported no supported formats");

        Format = chooseSurfaceFormat();
        Configure();
        RequestAcquire();
    }

    public void Resize(uint width, uint height)
    {
        if (Width == width && Height == height) return;

        WaitForAcquire();
        ReleasePreparedTexture();
        ReleaseFrameTexture();

        Width = width;
        Height = height;
        if (backend.DeviceHandle is not null)
        {
            Configure();
            RequestAcquire();
        }
    }

    public bool RequestAcquire()
    {
        if (backend.IsDisposed ||
            backend.IsDeviceLost ||
            Surface.IsNull ||
            Width == 0 ||
            Height == 0)
            return false;

        startAcquireThread();

        lock (sync)
        {
            if (acquireStop ||
                acquireInFlight ||
                acquireHasResult ||
                submissionInFlight ||
                !frameTexture.IsNull ||
                !frameTextureView.IsNull ||
                !preparedTexture.IsNull ||
                !preparedTextureView.IsNull)
                return false;

            acquireInFlight = true;
            acquireReady.Reset();
        }

        acquireRequested.Release();
        return true;
    }

    public bool TryPrepareFrameTexture(bool waitForAcquire = false)
    {
        while (true)
        {
            if (!frameTextureView.IsNull)
                return true;

            Exception exception = null;
            var status = default(WGPUSurfaceGetCurrentTextureStatus);
            var hasResult = false;
            var shouldRequest = false;
            var shouldWait = false;

            lock (sync)
            {
                if (!preparedTextureView.IsNull)
                {
                    frameTexture = preparedTexture;
                    frameTextureView = preparedTextureView;
                    preparedTexture = default;
                    preparedTextureView = default;
                    acquireHasResult = false;
                    acquireException = null;
                    return true;
                }

                if (acquireHasResult)
                {
                    hasResult = true;
                    status = preparedStatus;
                    exception = acquireException;
                    acquireHasResult = false;
                    acquireException = null;
                }
                else
                {
                    shouldRequest = !acquireInFlight;
                    shouldWait = acquireInFlight || submissionInFlight;
                }
            }

            if (exception is not null)
            {
                backend.MarkDeviceLost(exception);
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            if (hasResult)
            {
                if (status is WGPUSurfaceGetCurrentTextureStatus.Timeout or WGPUSurfaceGetCurrentTextureStatus.Outdated)
                {
                    configured = false;
                    Configure(true);
                }

                RequestAcquire();
                if (!waitForAcquire || status == WGPUSurfaceGetCurrentTextureStatus.Lost)
                    return false;

                WaitForAcquire();
                continue;
            }

            if (shouldRequest && RequestAcquire())
                shouldWait = true;

            if (!waitForAcquire || !shouldWait)
                return false;

            WaitForAcquire();
        }
    }

    public WebGpuSurfaceFrame DetachFrameTexture(bool present)
    {
        var texture = frameTexture;
        var textureView = frameTextureView;
        var shouldPresent = present && !texture.IsNull;

        frameTexture = default;
        frameTextureView = default;
        if (shouldPresent)
            SetSubmissionInFlight();

        return new(texture, textureView, shouldPresent);
    }

    public void PresentSubmittedTexture(Texture texture, TextureView textureView)
    {
        if (texture.IsNull) return;

        try
        {
            if (!backend.IsDeviceLost)
                Surface.Present();
        }
        catch (Exception ex)
        {
            backend.MarkDeviceLost(ex);
            throw;
        }
        finally
        {
            ReleaseTexture(texture, textureView);
            lock (sync)
            {
                submissionInFlight = false;
                acquireReady.Set();
            }
        }

        RequestAcquire();
    }

    public void ReleaseTexture(Texture texture, TextureView textureView)
    {
        if (backend.IsDeviceLost) return;

        try
        {
            if (!textureView.IsNull)
                textureView.Dispose();

            if (!texture.IsNull)
                texture.Dispose();
        }
        catch (Exception ex)
        {
            backend.MarkDeviceLost(ex);
        }
    }

    public void CompleteSubmission(Texture texture, TextureView textureView)
    {
        ReleaseTexture(texture, textureView);
        lock (sync)
        {
            submissionInFlight = false;
            acquireReady.Set();
        }

        RequestAcquire();
    }

    public void DropAfterDeviceLoss()
    {
        lock (sync)
        {
            acquireHasResult = false;
            acquireException = null;
            preparedTexture = default;
            preparedTextureView = default;
            submissionInFlight = false;
        }
    }

    public void ReleaseFrameTexture()
    {
        ReleaseTexture(frameTexture, frameTextureView);
        frameTexture = default;
        frameTextureView = default;
    }

    public void ReleasePreparedTexture()
    {
        Texture texture;
        TextureView textureView;
        lock (sync)
        {
            texture = preparedTexture;
            textureView = preparedTextureView;
            preparedTexture = default;
            preparedTextureView = default;
            acquireHasResult = false;
            acquireException = null;
        }

        ReleaseTexture(texture, textureView);
    }

    public void WaitForAcquire()
    {
        while (true)
        {
            lock (sync)
            {
                if (!acquireInFlight && !submissionInFlight)
                    return;
            }

            acquireReady.Wait();
        }
    }

    void SetSubmissionInFlight()
    {
        lock (sync)
        {
            submissionInFlight = true;
            acquireReady.Reset();
        }
    }

    void startAcquireThread()
    {
        if (acquireThread is not null)
            return;

        acquireThread = new(acquireLoop)
        {
            IsBackground = true,
            Name = "storybrew WebGPU surface acquire"
        };

        acquireThread.Start();
    }

    void acquireLoop()
    {
        while (true)
        {
            acquireRequested.Wait();

            lock (sync)
            {
                if (acquireStop)
                    return;
            }

            Texture texture = default;
            TextureView textureView = default;
            Exception exception = null;
            var status = WGPUSurfaceGetCurrentTextureStatus.SuccessOptimal;

            try
            {
                status = tryAcquireTexture(out texture);
                if (!texture.IsNull)
                    textureView = createFrameTextureView(texture);
            }
            catch (Exception ex)
            {
                if (!textureView.IsNull)
                    textureView.Dispose();

                if (!texture.IsNull)
                    texture.Dispose();

                texture = default;
                textureView = default;
                exception = ex;
            }

            lock (sync)
            {
                acquireInFlight = false;
                acquireHasResult = true;
                preparedStatus = status;
                acquireException = exception;
                preparedTexture = texture;
                preparedTextureView = textureView;
            }

            acquireReady.Set();
        }
    }

    WGPUSurfaceGetCurrentTextureStatus tryAcquireTexture(out Texture texture)
    {
        var result = Surface.GetCurrentTexture();
        texture = default;
        switch (result.Status)
        {
            case WGPUSurfaceGetCurrentTextureStatus.SuccessOptimal:
            case WGPUSurfaceGetCurrentTextureStatus.SuccessSuboptimal:
                texture = result.Texture;
                return result.Status;

            case WGPUSurfaceGetCurrentTextureStatus.Timeout:
            case WGPUSurfaceGetCurrentTextureStatus.Outdated:
            case WGPUSurfaceGetCurrentTextureStatus.Lost:
                return result.Status;

            case WGPUSurfaceGetCurrentTextureStatus.Error:
            default:
                throw new InvalidOperationException($"Unexpected WebGPU surface texture status: {result.Status}");
        }
    }

    TextureView createFrameTextureView(Texture texture)
    {
        TextureViewDescriptor descriptor = new()
        {
            Format = Format,
            Dimension = WGPUTextureViewDimension._2D,
            MipLevelCount = 1,
            ArrayLayerCount = 1,
            Aspect = WGPUTextureAspect.All,
            Usage = TextureUsage.RenderAttachment
        };

        return texture.CreateView(in descriptor);
    }

    void Configure(bool force = false)
    {
        if (Width == 0 || Height == 0) return;

        const WGPUPresentMode presentMode = WGPUPresentMode.Fifo;
        var alphaMode = chooseCompositeAlphaMode();
        if (!force &&
            configured &&
            configuredWidth == Width &&
            configuredHeight == Height &&
            configuredFormat == Format &&
            configuredPresentMode == presentMode &&
            configuredAlphaMode == alphaMode)
            return;

        ConfigureSurface(presentMode, alphaMode);

        configured = true;
        configuredWidth = Width;
        configuredHeight = Height;
        configuredFormat = Format;
        configuredPresentMode = presentMode;
        configuredAlphaMode = alphaMode;
        SDL.LogInfo(LogCategory.Render,
            $"WebGPU surface: {Width}x{Height}; format={Format}; present={presentMode}; alpha={alphaMode}; latency={desiredMaximumFrameLatency}");
    }

    void ConfigureSurface(WGPUPresentMode presentMode, WGPUCompositeAlphaMode alphaMode)
    {
        desiredMaximumFrameLatency = 2;

        var extras = new WGPUSurfaceConfigurationExtras
        {
            chain = new()
            {
                sType = (WGPUSType)WGPUNativeSType.WGPUSType_SurfaceConfigurationExtras
            },
            desiredMaximumFrameLatency = desiredMaximumFrameLatency
        };

        var viewFormat = Format;
        var descriptor = new WGPUSurfaceConfiguration
        {
            nextInChain = (WGPUChainedStruct*)&extras,
            device = backend.DeviceHandle.Handle,
            format = Format,
            usage = (ulong)TextureUsage.RenderAttachment,
            width = Width,
            height = Height,
            viewFormatCount = 1,
            viewFormats = &viewFormat,
            presentMode = presentMode,
            alphaMode = alphaMode
        };

        WGPU.wgpuSurfaceConfigure(Surface.Handle, &descriptor);
    }

    WGPUTextureFormat chooseSurfaceFormat()
    {
        if (DrawState.UseSrgb)
        {
            if (supportsSurfaceFormat(WGPUTextureFormat.BGRA8UnormSrgb))
                return WGPUTextureFormat.BGRA8UnormSrgb;

            if (supportsSurfaceFormat(WGPUTextureFormat.RGBA8UnormSrgb))
                return WGPUTextureFormat.RGBA8UnormSrgb;
        }

        if (supportsSurfaceFormat(WGPUTextureFormat.BGRA8Unorm))
            return WGPUTextureFormat.BGRA8Unorm;

        if (supportsSurfaceFormat(WGPUTextureFormat.RGBA8Unorm))
            return WGPUTextureFormat.RGBA8Unorm;

        if (supportsSurfaceFormat(WGPUTextureFormat.BGRA8UnormSrgb))
            return WGPUTextureFormat.BGRA8UnormSrgb;

        if (supportsSurfaceFormat(WGPUTextureFormat.RGBA8UnormSrgb))
            return WGPUTextureFormat.RGBA8UnormSrgb;

        return capabilities.Formats[0];
    }

    bool supportsSurfaceFormat(WGPUTextureFormat format)
        => capabilities.SupportsFormat(format);

    WGPUCompositeAlphaMode chooseCompositeAlphaMode()
    {
        var auto = WGPUCompositeAlphaMode.Auto;
        var opaque = WGPUCompositeAlphaMode.Opaque;

        if (supportsCompositeAlphaMode(opaque))
            return opaque;

        if (supportsCompositeAlphaMode(auto))
            return auto;

        return capabilities.AlphaModes.Length != 0
            ? capabilities.AlphaModes[0]
            : auto;
    }

    bool supportsCompositeAlphaMode(WGPUCompositeAlphaMode mode)
        => capabilities.SupportsAlphaMode(mode);
}