namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Buffers;
using Ahjo.Wgpu;
using WgpuBuffer = Ahjo.Wgpu.Buffer;

readonly struct WebGpuVertexUpload(WgpuBuffer buffer, IMemoryOwner<byte> data, int length)
{
    public readonly WgpuBuffer Buffer = buffer;
    public readonly IMemoryOwner<byte> Data = data;
    public readonly int Length = length;

    public bool IsEmpty => Data is null || Length <= 0;

    public void Flush(WebGpuVertexStagingBelt stagingBelt, CommandEncoder encoder)
    {
        if (IsEmpty) return;

        var alignedSize = (ulong)(Length + 3 & ~3);
        var span = stagingBelt.WriteBuffer(encoder, Buffer, alignedSize);
        Data.Memory.Span[..Length].CopyTo(span);
    }

    public void Release()
    {
        if (!IsEmpty)
            Data.Dispose();
    }
}