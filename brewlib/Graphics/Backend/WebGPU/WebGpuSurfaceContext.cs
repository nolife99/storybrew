namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using SDL3;
using Surface = Ahjo.Wgpu.Surface;

sealed class WebGpuSurfaceContext : IDisposable
{
    readonly Adapter adapter;
    readonly Device device;
    readonly IntPtr sdlWindow;

    readonly Surface surface;

    bool disposed;

    public WebGpuSurfaceContext(IntPtr sdlWindow,
        Surface surface,
        Device device,
        Adapter adapter,
        WGPUTextureFormat surfaceFormat,
        WGPUPresentMode presentMode,
        WGPUCompositeAlphaMode alphaMode)
    {
        this.sdlWindow = sdlWindow;
        this.surface = surface;
        this.device = device;
        this.adapter = adapter;
        SurfaceFormat = surfaceFormat;
        PresentMode = presentMode;
        AlphaMode = alphaMode;
    }

    public WGPUTextureFormat SurfaceFormat { get; }
    public WGPUPresentMode PresentMode { get; }
    public WGPUCompositeAlphaMode AlphaMode { get; }

    public uint Width { get; private set; }
    public uint Height { get; private set; }

    public Texture CurrentTexture { get; private set; }
    public TextureView CurrentView { get; private set; }

    public bool HasActiveFrame { get; private set; }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;

        if (HasActiveFrame)
        {
            CurrentView.Dispose();
            CurrentTexture.Dispose();
            HasActiveFrame = false;
        }

        surface.Dispose();
    }

    public void Configure()
    {
        QueryPixelSize(out var w, out var h);
        if (w == 0 || h == 0)
        {
            Width = 0;
            Height = 0;
            return;
        }

        Width = w;
        Height = h;
        surface.Configure(device, SurfaceFormat, TextureUsage.RenderAttachment, w, h, PresentMode, AlphaMode);
    }

    void QueryPixelSize(out uint width, out uint height)
    {
        width = 0;
        height = 0;
        if (sdlWindow == IntPtr.Zero) return;

        if (!SDL.GetWindowSizeInPixels(sdlWindow, out var w, out var h))
        {
            SDL.LogWarn(LogCategory.Video, $"SDL_GetWindowSizeInPixels failed: {SDL.GetError()}");
            return;
        }

        if (w > 0) width = (uint)w;
        if (h > 0) height = (uint)h;
    }

    public bool BeginFrame()
    {
        if (HasActiveFrame)
            throw new InvalidOperationException("BeginFrame called while a frame was already active");

        QueryPixelSize(out var w, out var h);
        if (w == 0 || h == 0)
        {
            return false;
        }

        if (w != Width || h != Height)
            Configure();

        var result = surface.GetCurrentTexture();
        if (TryHandleAcquire(in result, out var usable))
        {
            if (!usable) return false;
        }
        else
        {
            Configure();
            if (Width == 0 || Height == 0) return false;

            result = surface.GetCurrentTexture();
            if (!TryHandleAcquire(in result, out usable) || !usable)
                return false;
        }

        CurrentTexture = result.Texture;
        CurrentView = CurrentTexture.CreateView();
        HasActiveFrame = true;
        return true;
    }

    static bool TryHandleAcquire(in SurfaceAcquireResult result, out bool usable)
    {
        usable = false;
        switch (result.Status)
        {
            case WGPUSurfaceGetCurrentTextureStatus.SuccessOptimal:
            case WGPUSurfaceGetCurrentTextureStatus.SuccessSuboptimal:
                usable = true;
                return true;

            case WGPUSurfaceGetCurrentTextureStatus.Outdated:
            case WGPUSurfaceGetCurrentTextureStatus.Lost:
                result.Texture.Dispose();
                return false;

            case WGPUSurfaceGetCurrentTextureStatus.Timeout:
                SDL.LogWarn(LogCategory.Video, "Surface acquire timed out");
                result.Texture.Dispose();
                return true;

            default:
                SDL.LogError(LogCategory.Video, $"Surface acquire failed: {result.Status}");
                result.Texture.Dispose();
                return true;
        }
    }

    public void EndFrame()
    {
        if (!HasActiveFrame) return;

        HasActiveFrame = false;
        surface.Present();

        CurrentView.Dispose();
        CurrentView = default;

        CurrentTexture.Dispose();
        CurrentTexture = default;
    }
}