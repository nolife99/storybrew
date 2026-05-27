namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Ahjo.Wgpu;
using WgpuBuffer = Ahjo.Wgpu.Buffer;

sealed class WebGpuRenderPassState
{
    public BindGroupBinding[] BindGroups = new BindGroupBinding[4];
    public RenderPipeline Pipeline;
    public VertexBufferBinding[] VertexBuffers = new VertexBufferBinding[8];

    public void Reset()
    {
        Pipeline = default;
        Array.Clear(BindGroups);
        Array.Clear(VertexBuffers);
    }

    public void EnsureBindGroupSlot(int slot)
    {
        if (slot < BindGroups.Length) return;

        var length = BindGroups.Length;
        while (length <= slot)
            length *= 2;

        Array.Resize(ref BindGroups, length);
    }

    public void EnsureVertexBufferSlot(int slot)
    {
        if (slot < VertexBuffers.Length) return;

        var length = VertexBuffers.Length;
        while (length <= slot)
            length *= 2;

        Array.Resize(ref VertexBuffers, length);
    }

    public struct BindGroupBinding
    {
        public BindGroup BindGroup;
        public uint DynamicOffsetCount;
        public uint DynamicOffset;
    }

    public struct VertexBufferBinding
    {
        public WgpuBuffer Buffer;
        public ulong Offset;
        public ulong Size;
    }
}