namespace BrewLib.Graphics.Backend.WebGPU;

using System;

sealed class WebGpuUploadFence(WebGpuGraphicsBackend backend, uint frameSerial) : IGpuUploadFence
{
    public bool IsSignaled => backend.IsFrameFenceSignaled(frameSerial);
    public bool CanWait => false;
    public void Wait() => throw new NotSupportedException("WebGPU upload fences do not block on the render thread");
    public void Dispose() { }
}
