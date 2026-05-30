namespace BrewLib.Graphics.Backend.WebGPU;

using System;

/// <summary>
///     A single process-wide dynamic uniform ring shared by every pipeline's dynamic-offset uniform path, instead of
///     one ring buffer per render pipeline.
///
///     The trace captures showed the failure mode this addresses: a tiny long-lived uniform buffer (≈1.5 KiB),
///     allocated after the texture-load churn, suballocated into a freed-but-cached device-local block and pinned the
///     whole 256 MiB allocation. With one ring per pipeline, every pipeline contributes another such buffer, each a
///     candidate to strand a large gpu-allocator block. Consolidating to a single buffer that is created <b>early</b>
///     (before any texture loading) keeps the count of long-lived device-local uniform suballocations at one and gives
///     it an early, low placement rather than letting it land in a block vacated by transient textures.
///
///     Each draw sub-allocates a slot at the device's minimum uniform offset alignment; the cursor is reset once per
///     frame. The buffer grows List-style (doubling) and is never pre-sized to a fixed maximum. A mid-frame grow is
///     safe: it is the same reallocation the per-pipeline path already performed (the underlying
///     <see cref="WebGpuGraphicsBuffer" /> bumps its generation and raises <c>Resized</c>, evicting dependent bind
///     groups), and earlier draws in the frame keep binding the prior buffer, which stays alive — fenced to that
///     frame's submit — so both remain valid.
/// </summary>
sealed class WebGpuUniformRing : IDisposable
{
    const int InitialBytes = 1 * 1024 * 1024;

    readonly WebGpuGraphicsBuffer buffer;
    int frameCursor;
    bool disposed;

    public WebGpuUniformRing(WebGpuBackend backend, WebGpuDeviceContext deviceContext)
    {
        buffer = new(backend,
            deviceContext,
            new("GlobalUniformRing",
                GraphicsBufferTarget.Uniform,
                GraphicsBufferUsage.Stream,
                InitialBytes));
    }

    public WebGpuGraphicsBuffer Buffer => buffer;

    /// <summary>Reset the per-frame sub-allocation cursor. Called once at frame start.</summary>
    public void ResetFrame() => frameCursor = 0;

    /// <summary>
    ///     Reserve an aligned slot for one draw's uniform and return the buffer plus its byte offset. Grows the
    ///     underlying buffer (doubling) only if a frame's uniform traffic exceeds the current capacity.
    /// </summary>
    public (WebGpuGraphicsBuffer Buffer, uint Offset) Allocate(uint alignedSlotSize)
    {
        var offset = frameCursor;
        var next = checked(offset + (int)alignedSlotSize);

        if (next > buffer.CapacityBytes)
        {
            var cap = Math.Max(buffer.CapacityBytes, InitialBytes);
            while (cap < next) cap = checked(cap * 2);
            buffer.Allocate(cap);
        }

        frameCursor = next;
        return (buffer, (uint)offset);
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        buffer.Dispose();
    }
}
