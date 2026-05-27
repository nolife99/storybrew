namespace BrewLib.Graphics.Backend.WebGPU;

using System.Buffers;
using WgpuBuffer = Ahjo.Wgpu.Buffer;

readonly struct WebGpuBufferUpload(WgpuBuffer buffer, IMemoryOwner<byte> data, int offset, int length)
{
    public bool IsEmpty => data is null || length <= 0;

    public void Flush(WebGpuGraphicsBackend backend)
    {
        if (IsEmpty) return;

        try
        {
            backend.QueueHandle.WriteBuffer(buffer, (ulong)offset, data.Memory.Span.Slice(offset, length));
        }
        finally
        {
            Release();
        }
    }

    public void Release()
    {
        if (!IsEmpty)
            data.Dispose();
    }
}