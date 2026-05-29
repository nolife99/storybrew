namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Ahjo.Wgpu;
using SDL3;

static class SdlSurfaceFactory
{
    const string WindowsHwndKey = "SDL.window.win32.hwnd";
    const string WindowsHinstanceKey = "SDL.window.win32.instance";
    const string X11DisplayKey = "SDL.window.x11.display";
    const string X11WindowKey = "SDL.window.x11.window";
    const string WaylandDisplayKey = "SDL.window.wayland.display";
    const string WaylandSurfaceKey = "SDL.window.wayland.surface";
    const string CocoaWindowKey = "SDL.window.cocoa.window";
    const string MetalLayerKey = "SDL.window.uikit.metal_view";
    const string MetalLayerCocoaKey = "SDL.window.cocoa.metal_view";
    const string AndroidWindowKey = "SDL.window.android.window";

    public static SurfaceSource Create(IntPtr window)
    {
        if (window == IntPtr.Zero)
            throw new ArgumentException("SDL window handle is null", nameof(window));

        var props = SDL.GetWindowProperties(window);
        if (props == 0)
            throw new InvalidOperationException(
                $"SDL_GetWindowProperties returned 0: {SDL.GetError()}");

        if (TryWindows(props, out var source) || TryWayland(props, out source) || TryX11(props, out source) || TryAppleMetal(props, out source) || TryAndroid(props, out source))
            return source;

        var platform = SDL.GetPlatform();
        throw new PlatformNotSupportedException(
            $"WebGPU surface creation is not supported on this SDL window. Platform reported: {platform}. " +
            "No matching native handle properties were found.");
    }

    static bool TryWindows(uint props, out SurfaceSource source)
    {
        var hwnd = SDL.GetPointerProperty(props, WindowsHwndKey, IntPtr.Zero);
        if (hwnd == IntPtr.Zero)
        {
            source = default;
            return false;
        }

        var hinstance = SDL.GetPointerProperty(props, WindowsHinstanceKey, IntPtr.Zero);
        source = SurfaceSource.WindowsHwnd(hinstance, hwnd);
        return true;
    }

    static bool TryWayland(uint props, out SurfaceSource source)
    {
        var display = SDL.GetPointerProperty(props, WaylandDisplayKey, IntPtr.Zero);
        var surface = SDL.GetPointerProperty(props, WaylandSurfaceKey, IntPtr.Zero);
        if (display == IntPtr.Zero || surface == IntPtr.Zero)
        {
            source = default;
            return false;
        }

        source = SurfaceSource.WaylandSurface(display, surface);
        return true;
    }

    static bool TryX11(uint props, out SurfaceSource source)
    {
        var display = SDL.GetPointerProperty(props, X11DisplayKey, IntPtr.Zero);
        var window = SDL.GetNumberProperty(props, X11WindowKey, 0);
        if (display == IntPtr.Zero || window == 0)
        {
            source = default;
            return false;
        }

        source = SurfaceSource.XlibWindow(display, (ulong)window);
        return true;
    }

    static bool TryAppleMetal(uint props, out SurfaceSource source)
    {
        var metal = SDL.GetPointerProperty(props, MetalLayerCocoaKey, IntPtr.Zero);
        if (metal == IntPtr.Zero)
            metal = SDL.GetPointerProperty(props, MetalLayerKey, IntPtr.Zero);

        if (metal == IntPtr.Zero)
        {
            source = default;
            return false;
        }

        source = SurfaceSource.MetalLayer(metal);
        return true;
    }

    static bool TryAndroid(uint props, out SurfaceSource source)
    {
        var window = SDL.GetPointerProperty(props, AndroidWindowKey, IntPtr.Zero);
        if (window == IntPtr.Zero)
        {
            source = default;
            return false;
        }

        source = SurfaceSource.AndroidNativeWindow(window);
        return true;
    }
}