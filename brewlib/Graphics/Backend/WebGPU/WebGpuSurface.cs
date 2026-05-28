namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using SDL3;
using Surface = Ahjo.Wgpu.Surface;

unsafe sealed class WebGpuSurfaceManager : IDisposable
{
    readonly WebGpuGraphicsBackend backend;
    readonly Instance instance;
    bool configured, disposed;
    nint metalView;
    SurfaceCapabilities capabilities;
    WGPUCompositeAlphaMode configuredAlphaMode;
    WGPUTextureFormat configuredFormat;
    WGPUPresentMode configuredPresentMode;
    uint configuredWidth, configuredHeight;

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
    public bool HasSurface => !Surface.IsNull;
    public bool IsFormatSrgb => Format is WGPUTextureFormat.BGRA8UnormSrgb or WGPUTextureFormat.RGBA8UnormSrgb;

    public void Initialize(Adapter adapter)
    {
        capabilities = Surface.GetCapabilities(adapter);
        if (capabilities.Formats.Length == 0)
            throw new InvalidOperationException("WebGPU surface reported no supported formats");

        Format = chooseSurfaceFormat();
        Configure();
    }

    public void Resize(uint width, uint height)
    {
        if (Width == width && Height == height) return;
        Width = width;
        Height = height;
        if (backend.DeviceHandle is not null)
            Configure(true);
    }

    public WebGpuSurfaceFrame AcquireFrame(bool reconfigureOnFailure)
    {
        if (disposed || Surface.IsNull || Width == 0 || Height == 0)
            return null;

        Configure();

        try
        {
            var result = Surface.GetCurrentTexture();
            switch (result.Status)
            {
                case WGPUSurfaceGetCurrentTextureStatus.SuccessOptimal:
                case WGPUSurfaceGetCurrentTextureStatus.SuccessSuboptimal:
                    if (result.Texture.IsNull) return null;
                    return new(this, result.Texture, createFrameTextureView(result.Texture));

                case WGPUSurfaceGetCurrentTextureStatus.Timeout:
                    return null;

                case WGPUSurfaceGetCurrentTextureStatus.Outdated:
                    if (reconfigureOnFailure)
                        Configure(true);
                    return null;

                case WGPUSurfaceGetCurrentTextureStatus.Lost:
                case WGPUSurfaceGetCurrentTextureStatus.Error:
                default:
                    throw new WebGpuException($"WebGPU surface acquisition failed with status {result.Status}");
            }
        }
        catch (Exception ex)
        {
            throw WebGpuException.Fatal("acquiring WebGPU surface texture", ex);
        }
    }

    public void Present(Texture texture, TextureView textureView)
    {
        try
        {
            if (!texture.IsNull)
                Surface.Present();
        }
        catch (Exception ex)
        {
            throw WebGpuException.Fatal("presenting WebGPU surface texture", ex);
        }
        finally
        {
            Release(texture, textureView);
        }
    }

    public void Release(Texture texture, TextureView textureView)
    {
        try
        {
            if (!textureView.IsNull)
                textureView.Dispose();
            if (!texture.IsNull)
                texture.Dispose();
        }
        catch (Exception ex)
        {
            throw WebGpuException.Fatal("releasing WebGPU surface texture", ex);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

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
        SDL.LogInfo(LogCategory.Render, $"WebGPU surface: {Width}x{Height}; format={Format}; present={presentMode}; alpha={alphaMode}");
    }

    void ConfigureSurface(WGPUPresentMode presentMode, WGPUCompositeAlphaMode alphaMode)
    {
        var extras = new WGPUSurfaceConfigurationExtras
        {
            chain = new()
            {
                sType = (WGPUSType)WGPUNativeSType.WGPUSType_SurfaceConfigurationExtras
            },
            desiredMaximumFrameLatency = 2
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
            if (supportsSurfaceFormat(WGPUTextureFormat.BGRA8UnormSrgb)) return WGPUTextureFormat.BGRA8UnormSrgb;
            if (supportsSurfaceFormat(WGPUTextureFormat.RGBA8UnormSrgb)) return WGPUTextureFormat.RGBA8UnormSrgb;
        }

        if (supportsSurfaceFormat(WGPUTextureFormat.BGRA8Unorm)) return WGPUTextureFormat.BGRA8Unorm;
        if (supportsSurfaceFormat(WGPUTextureFormat.RGBA8Unorm)) return WGPUTextureFormat.RGBA8Unorm;
        if (supportsSurfaceFormat(WGPUTextureFormat.BGRA8UnormSrgb)) return WGPUTextureFormat.BGRA8UnormSrgb;
        if (supportsSurfaceFormat(WGPUTextureFormat.RGBA8UnormSrgb)) return WGPUTextureFormat.RGBA8UnormSrgb;
        return capabilities.Formats[0];
    }

    bool supportsSurfaceFormat(WGPUTextureFormat format)
    {
        foreach (var supported in capabilities.Formats)
            if (supported == format)
                return true;
        return false;
    }

    WGPUCompositeAlphaMode chooseCompositeAlphaMode()
    {
        var opaque = WGPUCompositeAlphaMode.Opaque;
        var auto = WGPUCompositeAlphaMode.Auto;

        if (supportsCompositeAlphaMode(opaque))
            return opaque;

        if (supportsCompositeAlphaMode(auto))
            return auto;

        return capabilities.AlphaModes.Length != 0
            ? capabilities.AlphaModes[0]
            : auto;
    }

    bool supportsCompositeAlphaMode(WGPUCompositeAlphaMode mode)
    {
        foreach (var supported in capabilities.AlphaModes)
            if (supported == mode)
                return true;

        return false;
    }
}

sealed class WebGpuSurfaceFrame : IDisposable
{
    readonly WebGpuSurfaceManager manager;
    bool disposed;

    public WebGpuSurfaceFrame(WebGpuSurfaceManager manager, Texture texture, TextureView textureView)
    {
        this.manager = manager;
        Texture = texture;
        TextureView = textureView;
    }

    public Texture Texture { get; private set; }
    public TextureView TextureView { get; private set; }

    public void Present()
    {
        if (disposed) return;
        try { manager.Present(Texture, TextureView); }
        finally { Clear(); }
    }

    public void Dispose()
    {
        if (disposed) return;
        try { manager.Release(Texture, TextureView); }
        finally { Clear(); }
    }

    void Clear()
    {
        Texture = default;
        TextureView = default;
        disposed = true;
    }
}

static class WebGpuSurfaceFactory
{
    public static Surface Create(Instance instance, nint window, out nint metalView)
    {
        metalView = 0;
        var properties = SDL.GetWindowProperties(window);
        if (properties == 0)
            throw new InvalidOperationException($"Unable to query SDL window properties: {SDL.GetError()}");

        var createdSurface = SDL.GetCurrentVideoDriver() switch
        {
            "windows" or "win32" => createWin32Surface(instance, properties),
            "cocoa" or "uikit" => createMetalSurface(instance, window, ref metalView),
            "wayland" => createWaylandSurface(instance, properties),
            "x11" => createXlibSurface(instance, properties),
            "android" => createAndroidSurface(instance, properties),
            _ => tryCreateKnownSurface(instance, properties)
        };

        return !createdSurface.IsNull
            ? createdSurface
            : throw new PlatformNotSupportedException($"No WebGPU surface path is available for SDL video driver '{SDL.GetCurrentVideoDriver() ?? "unknown"}'");
    }

    static Surface tryCreateKnownSurface(Instance instance, uint properties)
    {
        var surface = createWin32Surface(instance, properties);
        if (!surface.IsNull) return surface;
        surface = createWaylandSurface(instance, properties);
        if (!surface.IsNull) return surface;
        surface = createXlibSurface(instance, properties);
        return !surface.IsNull ? surface : createAndroidSurface(instance, properties);
    }

    static Surface createWin32Surface(Instance instance, uint properties)
    {
        var hwnd = SDL.GetPointerProperty(properties, SDL.Props.WindowWin32HWNDPointer, 0);
        var hinstance = SDL.GetPointerProperty(properties, SDL.Props.WindowWin32InstancePointer, 0);
        return hwnd == 0 || hinstance == 0 ? default : instance.CreateSurface(SurfaceSource.WindowsHwnd(hinstance, hwnd));
    }

    static Surface createMetalSurface(Instance instance, nint window, ref nint metalView)
    {
        metalView = SDL.MetalCreateView(window);
        if (metalView == 0) return default;

        var layer = SDL.MetalGetLayer(metalView);
        if (layer == 0)
        {
            SDL.MetalDestroyView(metalView);
            metalView = 0;
            return default;
        }

        try { return instance.CreateSurface(SurfaceSource.MetalLayer(layer)); }
        catch
        {
            SDL.MetalDestroyView(metalView);
            metalView = 0;
            throw;
        }
    }

    static Surface createWaylandSurface(Instance instance, uint properties)
    {
        var display = SDL.GetPointerProperty(properties, SDL.Props.WindowWaylandDisplayPointer, 0);
        var surface = SDL.GetPointerProperty(properties, SDL.Props.WindowWaylandSurfacePointer, 0);
        return display == 0 || surface == 0 ? default : instance.CreateSurface(SurfaceSource.WaylandSurface(display, surface));
    }

    static Surface createXlibSurface(Instance instance, uint properties)
    {
        var display = SDL.GetPointerProperty(properties, SDL.Props.WindowX11DisplayPointer, 0);
        var window = SDL.GetNumberProperty(properties, SDL.Props.WindowX11WindowNumber, 0);
        return display == 0 || window == 0 ? default : instance.CreateSurface(SurfaceSource.XlibWindow(display, (ulong)window));
    }

    static Surface createAndroidSurface(Instance instance, uint properties)
    {
        var window = SDL.GetPointerProperty(properties, SDL.Props.WindowAndroidWindowPointer, 0);
        return window == 0 ? default : instance.CreateSurface(SurfaceSource.AndroidNativeWindow(window));
    }
}
