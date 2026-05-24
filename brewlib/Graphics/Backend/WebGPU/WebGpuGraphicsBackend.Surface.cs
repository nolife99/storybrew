namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using SDL3;
using Silk.NET.WebGPU;
using WgpuSurface = Silk.NET.WebGPU.Surface;

public unsafe sealed partial class WebGpuGraphicsBackend
{
    WgpuSurface* createSurface(nint window)
    {
        var properties = SDL.GetWindowProperties(window);
        if (properties == 0)
            throw new InvalidOperationException($"Unable to get SDL window properties: {SDL.GetError()}");

        var videoDriver = SDL.GetCurrentVideoDriver();
        var createdSurface = videoDriver switch
        {
            "windows" or "win32" => createWin32Surface(properties),
            "cocoa" or "uikit" => createMetalSurface(window),
            "wayland" => createWaylandSurface(properties),
            "x11" => createXlibSurface(properties),
            "android" => createAndroidSurface(properties),
            _ => tryCreateKnownSurface(properties, window)
        };

        return createdSurface is not null
            ? createdSurface
            : throw new PlatformNotSupportedException(
                $"No WebGPU surface path is available for SDL video driver '{videoDriver ?? "unknown"}'");
    }

    WgpuSurface* tryCreateKnownSurface(uint properties, nint window)
    {
        var createdSurface = createWin32Surface(properties);
        if (createdSurface is not null) return createdSurface;

        createdSurface = createWaylandSurface(properties);
        if (createdSurface is not null) return createdSurface;

        createdSurface = createXlibSurface(properties);
        if (createdSurface is not null) return createdSurface;

        return createAndroidSurface(properties);
    }

    WgpuSurface* createWin32Surface(uint properties)
    {
        var hwnd = SDL.GetPointerProperty(properties, SDL.Props.WindowWin32HWNDPointer, 0);
        var hinstance = SDL.GetPointerProperty(properties, SDL.Props.WindowWin32InstancePointer, 0);
        if (hwnd == 0 || hinstance == 0)
            return null;

        SurfaceDescriptorFromWindowsHWND hwndDescriptor = new()
        {
            Chain = new()
            {
                SType = SType.SurfaceDescriptorFromWindowsHwnd
            },
            Hinstance = (void*)hinstance,
            Hwnd = (void*)hwnd
        };

        return createChainedSurface(&hwndDescriptor.Chain);
    }

    WgpuSurface* createMetalSurface(nint window)
    {
        metalView = SDL.MetalCreateView(window);
        if (metalView == 0)
            return null;

        var layer = SDL.MetalGetLayer(metalView);
        if (layer == 0)
        {
            SDL.MetalDestroyView(metalView);
            metalView = 0;
            return null;
        }

        SurfaceDescriptorFromMetalLayer metalDescriptor = new()
        {
            Chain = new()
            {
                SType = SType.SurfaceDescriptorFromMetalLayer
            },
            Layer = (void*)layer
        };

        try
        {
            return createChainedSurface(&metalDescriptor.Chain);
        }
        catch
        {
            SDL.MetalDestroyView(metalView);
            metalView = 0;
            throw;
        }
    }

    WgpuSurface* createWaylandSurface(uint properties)
    {
        var display = SDL.GetPointerProperty(properties, SDL.Props.WindowWaylandDisplayPointer, 0);
        var surface = SDL.GetPointerProperty(properties, SDL.Props.WindowWaylandSurfacePointer, 0);
        if (display == 0 || surface == 0)
            return null;

        SurfaceDescriptorFromWaylandSurface waylandDescriptor = new()
        {
            Chain = new()
            {
                SType = SType.SurfaceDescriptorFromWaylandSurface
            },
            Display = (void*)display,
            Surface = (void*)surface
        };

        return createChainedSurface(&waylandDescriptor.Chain);
    }

    WgpuSurface* createXlibSurface(uint properties)
    {
        var display = SDL.GetPointerProperty(properties, SDL.Props.WindowX11DisplayPointer, 0);
        var window = SDL.GetNumberProperty(properties, SDL.Props.WindowX11WindowNumber, 0);
        if (display == 0 || window == 0)
            return null;

        SurfaceDescriptorFromXlibWindow xlibDescriptor = new()
        {
            Chain = new()
            {
                SType = SType.SurfaceDescriptorFromXlibWindow
            },
            Display = (void*)display,
            Window = (ulong)window
        };

        return createChainedSurface(&xlibDescriptor.Chain);
    }

    WgpuSurface* createAndroidSurface(uint properties)
    {
        var window = SDL.GetPointerProperty(properties, SDL.Props.WindowAndroidWindowPointer, 0);
        if (window == 0)
            return null;

        SurfaceDescriptorFromAndroidNativeWindow androidDescriptor = new()
        {
            Chain = new()
            {
                SType = SType.SurfaceDescriptorFromAndroidNativeWindow
            },
            Window = (void*)window
        };

        return createChainedSurface(&androidDescriptor.Chain);
    }

    WgpuSurface* createChainedSurface(ChainedStruct* nextInChain)
    {
        SurfaceDescriptor descriptor = new()
        {
            NextInChain = nextInChain
        };

        var createdSurface = Api.InstanceCreateSurface(instance, in descriptor);
        return createdSurface is not null
            ? createdSurface
            : throw new InvalidOperationException("Unable to create WebGPU surface from SDL window");
    }
}