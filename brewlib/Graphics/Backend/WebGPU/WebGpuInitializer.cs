namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Ahjo.Wgpu;

static class WebGpuInitializer
{
    public static Instance CreateInstance()
    {
        var descriptor = new InstanceDescriptor
        {
            Backends = chooseInstanceBackends(),
#if DEBUG
            Flags = InstanceFlags.DevDefault
#else
            Flags = InstanceFlags.None
#endif
        };

        return Instance.Create(in descriptor);
    }

    static InstanceBackends chooseInstanceBackends()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10))
            return InstanceBackends.Dx12;

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS())
            return InstanceBackends.Metal;

        if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
            return InstanceBackends.Vulkan;

        return InstanceBackends.GL;
    }
}