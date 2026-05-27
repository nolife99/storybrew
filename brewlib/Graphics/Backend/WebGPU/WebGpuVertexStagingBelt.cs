namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Util;
using WgpuBuffer = Ahjo.Wgpu.Buffer;

sealed class WebGpuVertexStagingBelt : IDisposable
{
    const ulong DefaultChunkSize = 2 * 1024 * 1024;

    readonly StagingBelt belt;
    bool disposed;

    public WebGpuVertexStagingBelt(WebGpuGraphicsBackend backend, ulong chunkSize = DefaultChunkSize)
        => belt = new(backend.DeviceHandle, chunkSize);

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        belt.Dispose();
    }

    public Span<byte> WriteBuffer(CommandEncoder encoder, WgpuBuffer target, ulong size)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return belt.WriteBuffer(encoder, target, 0, size);
    }

    public void Finish()
    {
        if (!disposed)
            belt.Finish();
    }

    public void Recall()
    {
        if (!disposed)
            belt.Recall();
    }

    public void Poll()
    {
        if (!disposed)
            belt.Poll();
    }
}