namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Ahjo.Wgpu;
using SDL3;
using Surface = Ahjo.Wgpu.Surface;

static class WebGpuSurfaceFactory
{
    public static Surface Create(Instance instance, nint window, out nint metalView)
    {
        metalView = 0;
        var properties = SDL.GetWindowProperties(window);
        if (properties == 0)
            throw new InvalidOperationException($"Unable to get SDL window properties: {SDL.GetError()}");

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
            : throw new PlatformNotSupportedException(
                $"No WebGPU surface path is available for SDL video driver '{SDL.GetCurrentVideoDriver() ?? "unknown"}'");
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
        return hwnd == 0 || hinstance == 0
            ? default
            : instance.CreateSurface(SurfaceSource.WindowsHwnd(hinstance, hwnd));
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

        try
        {
            return instance.CreateSurface(SurfaceSource.MetalLayer(layer));
        }
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
        return display == 0 || surface == 0
            ? default
            : instance.CreateSurface(SurfaceSource.WaylandSurface(display, surface));
    }

    static Surface createXlibSurface(Instance instance, uint properties)
    {
        var display = SDL.GetPointerProperty(properties, SDL.Props.WindowX11DisplayPointer, 0);
        var window = SDL.GetNumberProperty(properties, SDL.Props.WindowX11WindowNumber, 0);
        return display == 0 || window == 0
            ? default
            : instance.CreateSurface(SurfaceSource.XlibWindow(display, (ulong)window));
    }

    static Surface createAndroidSurface(Instance instance, uint properties)
    {
        var window = SDL.GetPointerProperty(properties, SDL.Props.WindowAndroidWindowPointer, 0);
        return window == 0
            ? default
            : instance.CreateSurface(SurfaceSource.AndroidNativeWindow(window));
    }
}