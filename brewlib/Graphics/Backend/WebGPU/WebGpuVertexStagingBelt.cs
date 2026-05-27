namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Util;
using WgpuBuffer = Ahjo.Wgpu.Buffer;

// Wraps a StagingBelt dedicated to vertex buffer uploads within a single frame.
// All methods are called exclusively from the submission thread (EncodeRecordedFrame),
// so no locking is required. Lifecycle per frame:
//   1. WriteBuffer() - one call per batch (records CopyBufferToBuffer into the encoder)
//   2. Finish()      - seals all active chunks before the render pass begins
//   3. [submit]
//   4. Recall()      - begins async remap of submitted chunks
//   5. Poll()        - called from maintenance thread to complete remaps
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

    // Returns a span to write vertex data into. Encodes CopyBufferToBuffer into the encoder.
    // size must be 4-byte aligned (pad if needed).
    public Span<byte> WriteBuffer(CommandEncoder encoder, WgpuBuffer target, ulong size)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return belt.WriteBuffer(encoder, target, 0, size);
    }

    // Seals all active chunks. Call after all WriteBuffer calls, before BeginRenderPass.
    public void Finish()
    {
        if (!disposed)
            belt.Finish();
    }

    // Begins async remap. Call after QueueSubmit.
    public void Recall()
    {
        if (!disposed)
            belt.Recall();
    }

    // Completes async remaps. Called from DeferredReleaseManager maintenance.
    public void Poll()
    {
        if (!disposed)
            belt.Poll();
    }
}