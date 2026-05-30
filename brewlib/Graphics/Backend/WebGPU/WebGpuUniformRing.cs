namespace BrewLib.Graphics.Backend.WebGPU;

using System;

sealed class WebGpuUniformRing : IDisposable
{
    const int InitialBytes = 1 * 1024 * 1024;

    int frameCursor;
    bool disposed;

    public WebGpuUniformRing(WebGpuBackend backend, WebGpuDeviceContext deviceContext)
    {
        Buffer = new(backend,
            deviceContext,
            new("GlobalUniformRing",
                GraphicsBufferTarget.Uniform,
                GraphicsBufferUsage.Stream,
                InitialBytes));
    }

    public WebGpuGraphicsBuffer Buffer { get; }

    public void ResetFrame() => frameCursor = 0;

    public (WebGpuGraphicsBuffer Buffer, uint Offset) Allocate(uint alignedSlotSize)
    {
        var offset = frameCursor;
        var next = checked(offset + (int)alignedSlotSize);

        if (next > Buffer.CapacityBytes)
        {
            var cap = Math.Max(Buffer.CapacityBytes, InitialBytes);
            while (cap < next) cap = checked(cap * 2);
            Buffer.Allocate(cap);
        }

        frameCursor = next;
        return (Buffer, (uint)offset);
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        Buffer.Dispose();
    }
}