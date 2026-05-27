namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Concurrent;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using Tiny.PooledCollections.Generic.Value;
using WgpuBuffer = Ahjo.Wgpu.Buffer;

sealed class WebGpuFrameCommandList : IDisposable
{
    static readonly ConcurrentBag<WebGpuFrameCommandList> pool = [];

    ValueList<WebGpuFrameCommand> commands = ValueList.Create<WebGpuFrameCommand>();
    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        commands.Dispose();
        disposed = true;
    }

    public static WebGpuFrameCommandList Rent()
        => pool.TryTake(out var list) ? list : new();

    public void Clear()
        => commands.Clear();

    public void Release()
    {
        if (disposed) return;

        commands.Clear();
        pool.Add(this);
    }

    public void SetPipeline(RenderPipeline pipeline)
        => commands.Add(new()
        {
            Kind = WebGpuFrameCommandKind.SetPipeline,
            Pipeline = pipeline
        });

    public void SetBindGroup(uint slot, BindGroup bindGroup)
        => commands.Add(new()
        {
            Kind = WebGpuFrameCommandKind.SetBindGroup,
            Slot = slot,
            BindGroup = bindGroup
        });

    public void SetBindGroup(uint slot, BindGroup bindGroup, uint dynamicOffset)
        => commands.Add(new()
        {
            Kind = WebGpuFrameCommandKind.SetBindGroupDynamic,
            Slot = slot,
            BindGroup = bindGroup,
            DynamicOffset = dynamicOffset
        });

    public void SetVertexBuffer(uint slot, WgpuBuffer buffer, ulong offset, ulong size)
        => commands.Add(new()
        {
            Kind = WebGpuFrameCommandKind.SetVertexBuffer,
            Slot = slot,
            Buffer = buffer,
            Offset = offset,
            Size = size
        });

    public void SetIndexBuffer(WgpuBuffer buffer, WGPUIndexFormat format, ulong offset, ulong size)
        => commands.Add(new()
        {
            Kind = WebGpuFrameCommandKind.SetIndexBuffer,
            Buffer = buffer,
            IndexFormat = format,
            Offset = offset,
            Size = size
        });

    public void SetViewport(float x, float y, float width, float height, float minDepth, float maxDepth)
        => commands.Add(new()
        {
            Kind = WebGpuFrameCommandKind.SetViewport,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            MinDepth = minDepth,
            MaxDepth = maxDepth
        });

    public void SetScissorRect(uint x, uint y, uint width, uint height)
        => commands.Add(new()
        {
            Kind = WebGpuFrameCommandKind.SetScissorRect,
            XU = x,
            YU = y,
            WidthU = width,
            HeightU = height
        });

    public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
        => commands.Add(new()
        {
            Kind = WebGpuFrameCommandKind.Draw,
            Count = vertexCount,
            InstanceCount = instanceCount,
            First = firstVertex,
            FirstInstance = firstInstance
        });

    public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int baseVertex, uint firstInstance)
        => commands.Add(new()
        {
            Kind = WebGpuFrameCommandKind.DrawIndexed,
            Count = indexCount,
            InstanceCount = instanceCount,
            First = firstIndex,
            BaseVertex = baseVertex,
            FirstInstance = firstInstance
        });

    public void Replay(RenderPassEncoder pass)
    {
        foreach (var command in commands)
        {
            switch (command.Kind)
            {
                case WebGpuFrameCommandKind.SetPipeline:
                    pass.SetPipeline(command.Pipeline);
                    break;

                case WebGpuFrameCommandKind.SetBindGroup:
                    pass.SetBindGroup(command.Slot, command.BindGroup);
                    break;

                case WebGpuFrameCommandKind.SetBindGroupDynamic:
                    pass.SetBindGroup(command.Slot, command.BindGroup, [command.DynamicOffset]);
                    break;

                case WebGpuFrameCommandKind.SetVertexBuffer:
                    pass.SetVertexBuffer(command.Slot, command.Buffer, command.Offset, command.Size);
                    break;

                case WebGpuFrameCommandKind.SetIndexBuffer:
                    pass.SetIndexBuffer(command.Buffer, command.IndexFormat, command.Offset, command.Size);
                    break;

                case WebGpuFrameCommandKind.SetViewport:
                    pass.SetViewport(command.X, command.Y, command.Width, command.Height, command.MinDepth, command.MaxDepth);
                    break;

                case WebGpuFrameCommandKind.SetScissorRect:
                    pass.SetScissorRect(command.XU, command.YU, command.WidthU, command.HeightU);
                    break;

                case WebGpuFrameCommandKind.Draw:
                    pass.Draw(command.Count, command.InstanceCount, command.First, command.FirstInstance);
                    break;

                case WebGpuFrameCommandKind.DrawIndexed:
                    pass.DrawIndexed(command.Count, command.InstanceCount, command.First, command.BaseVertex, command.FirstInstance);
                    break;
            }
        }
    }

    enum WebGpuFrameCommandKind : byte
    {
        SetPipeline,
        SetBindGroup,
        SetBindGroupDynamic,
        SetVertexBuffer,
        SetIndexBuffer,
        SetViewport,
        SetScissorRect,
        Draw,
        DrawIndexed
    }

    struct WebGpuFrameCommand
    {
        public WebGpuFrameCommandKind Kind;
        public RenderPipeline Pipeline;
        public BindGroup BindGroup;
        public WgpuBuffer Buffer;
        public WGPUIndexFormat IndexFormat;
        public uint Slot, Count, InstanceCount, First, FirstInstance, DynamicOffset;
        public int BaseVertex;
        public ulong Offset, Size;
        public float X, Y, Width, Height, MinDepth, MaxDepth;
        public uint XU, YU, WidthU, HeightU;
    }
}