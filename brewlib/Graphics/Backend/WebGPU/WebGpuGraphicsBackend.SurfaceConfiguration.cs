namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using SDL3;
using Silk.NET.WebGPU;

public unsafe sealed partial class WebGpuGraphicsBackend
{
    void initializeSurface()
    {
        Api.SurfaceGetCapabilities(surface, adapter, ref surfaceCapabilities);
        if (surfaceCapabilities.FormatCount == 0)
            throw new InvalidOperationException("WebGPU surface reported no supported formats");

        SurfaceFormat = chooseSurfaceFormat();
        configureSurface();
        requestSurfaceTextureAcquire();
    }

    TextureFormat chooseSurfaceFormat()
    {
        if (supportsSurfaceFormat(TextureFormat.Bgra8Unorm))
            return TextureFormat.Bgra8Unorm;

        if (supportsSurfaceFormat(TextureFormat.Rgba8Unorm))
            return TextureFormat.Rgba8Unorm;

        if (supportsSurfaceFormat(TextureFormat.Bgra8UnormSrgb))
            return TextureFormat.Bgra8UnormSrgb;

        if (supportsSurfaceFormat(TextureFormat.Rgba8UnormSrgb))
            return TextureFormat.Rgba8UnormSrgb;

        return surfaceCapabilities.Formats[0];
    }

    bool supportsSurfaceFormat(TextureFormat format)
    {
        for (nuint i = 0; i < surfaceCapabilities.FormatCount; ++i)
            if (surfaceCapabilities.Formats[i] == format)
                return true;

        return false;
    }

    void configureSurface(bool force = false)
    {
        if (FramebufferWidth == 0 || FramebufferHeight == 0) return;

        var presentMode = choosePresentMode();
        var alphaMode = chooseCompositeAlphaMode();
        if (!force &&
            surfaceConfigured &&
            configuredSurfaceWidth == FramebufferWidth &&
            configuredSurfaceHeight == FramebufferHeight &&
            configuredSurfaceFormat == SurfaceFormat &&
            configuredPresentMode == presentMode &&
            configuredAlphaMode == alphaMode)
            return;

        surfaceConfiguration = new()
        {
            Usage = TextureUsage.RenderAttachment,
            Format = SurfaceFormat,
            PresentMode = presentMode,
            AlphaMode = alphaMode,
            Device = DeviceHandle,
            Width = FramebufferWidth,
            Height = FramebufferHeight
        };

        Api.SurfaceConfigure(surface, in surfaceConfiguration);
        surfaceConfigured = true;
        configuredSurfaceWidth = FramebufferWidth;
        configuredSurfaceHeight = FramebufferHeight;
        configuredSurfaceFormat = SurfaceFormat;
        configuredPresentMode = presentMode;
        configuredAlphaMode = alphaMode;
        SDL.LogInfo(LogCategory.Render,
            $"WebGPU surface: {FramebufferWidth}x{FramebufferHeight}; format={SurfaceFormat}; present={presentMode}; alpha={alphaMode}");
    }

    PresentMode choosePresentMode()
    {
        if (!preferLowLatency || surfaceCapabilities.PresentModeCount == 0)
            return PresentMode.Fifo;

        if (supportsPresentMode(PresentMode.Mailbox))
            return PresentMode.Mailbox;

        if (supportsPresentMode(PresentMode.Immediate))
            return PresentMode.Immediate;

        if (supportsPresentMode(PresentMode.FifoRelaxed))
            return PresentMode.FifoRelaxed;

        return PresentMode.Fifo;
    }

    bool supportsPresentMode(PresentMode mode)
    {
        for (nuint i = 0; i < surfaceCapabilities.PresentModeCount; ++i)
            if (surfaceCapabilities.PresentModes[i] == mode)
                return true;

        return false;
    }

    CompositeAlphaMode chooseCompositeAlphaMode()
    {
        if (supportsCompositeAlphaMode(CompositeAlphaMode.Opaque))
            return CompositeAlphaMode.Opaque;

        if (supportsCompositeAlphaMode(CompositeAlphaMode.Auto))
            return CompositeAlphaMode.Auto;

        return surfaceCapabilities.AlphaModeCount != 0
            ? surfaceCapabilities.AlphaModes[0]
            : CompositeAlphaMode.Auto;
    }

    bool supportsCompositeAlphaMode(CompositeAlphaMode mode)
    {
        for (nuint i = 0; i < surfaceCapabilities.AlphaModeCount; ++i)
            if (surfaceCapabilities.AlphaModes[i] == mode)
                return true;

        return false;
    }
}