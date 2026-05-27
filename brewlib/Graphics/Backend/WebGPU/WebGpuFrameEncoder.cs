namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Numerics;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using WgpuBuffer = Ahjo.Wgpu.Buffer;

sealed class WebGpuFrameEncoder(WebGpuGraphicsBackend backend, WebGpuGraphicsDevice graphicsDevice)
{
    Vector4 clearColor;
    WebGpuFrameCommandList commands = WebGpuFrameCommandList.Rent();
    bool hasFrame, hasRenderPass;

    public WebGpuRenderPassState State { get; } = new();

    public bool HasFrame => hasFrame && !backend.IsDeviceLost;
    public bool HasRenderPass => hasRenderPass && !backend.IsDeviceLost;
    public bool HasContents { get; private set; }

    public uint RenderPassSerial { get; private set; }

    public void Begin(Vector4 clearColor)
    {
        if (hasFrame)
            throw new InvalidOperationException("A WebGPU frame is already active");

        this.clearColor = clearColor;
        hasFrame = true;
        hasRenderPass = false;
        HasContents = false;
        commands.Clear();
        State.Reset();
    }

    public bool TryRequireRenderPass()
    {
        backend.ThrowIfDeviceLost();
        if (!HasFrame)
            throw new InvalidOperationException("WebGPU render pass requested outside an active frame");

        if (hasRenderPass)
            return true;

        hasRenderPass = true;
        HasContents = true;
        ++RenderPassSerial;
        State.Reset();
        graphicsDevice.ApplyRenderPassState();
        DrawState.CountDrawCall();
        return true;
    }

    public void EndRenderPass()
        => hasRenderPass = false;

    public WebGpuRecordedFrame End(bool presentSurfaceTexture, WebGpuFrameFlushes flushes)
    {
        if (!HasFrame)
            throw new InvalidOperationException("WebGPU frame is not active");

        var frameCommands = commands;
        commands = WebGpuFrameCommandList.Rent();
        var frame = new WebGpuRecordedFrame(frameCommands, flushes, clearColor, HasContents, presentSurfaceTexture);
        Reset();
        return frame;
    }

    public void Reset()
    {
        hasFrame = false;
        hasRenderPass = false;
        HasContents = false;
        State.Reset();
    }

    public void DropAfterDeviceLoss()
        => Reset();

    public void DisposeActiveHandles()
        => Reset();

    public void SetRenderPipeline(RenderPipeline pipeline)
    {
        if (State.Pipeline.NativeHandle() == pipeline.NativeHandle()) return;

        commands.SetPipeline(pipeline);
        State.Pipeline = pipeline;
    }

    public void SetBindGroup(uint slot, BindGroup bindGroup)
    {
        var index = checked((int)slot);
        if (index >= backend.MaxBindGroups)
            throw new InvalidOperationException($"WebGPU bind group slot {slot} exceeds device limit {backend.MaxBindGroups}");

        State.EnsureBindGroupSlot(index);
        ref var binding = ref State.BindGroups[index];
        if (binding.BindGroup.NativeHandle() == bindGroup.NativeHandle() && binding.DynamicOffsetCount == 0) return;

        commands.SetBindGroup(slot, bindGroup);
        binding.BindGroup = bindGroup;
        binding.DynamicOffsetCount = 0;
        binding.DynamicOffset = 0;
    }

    public void SetBindGroup(uint slot, BindGroup bindGroup, uint dynamicOffset)
    {
        var index = checked((int)slot);
        if (index >= backend.MaxBindGroups)
            throw new InvalidOperationException($"WebGPU bind group slot {slot} exceeds device limit {backend.MaxBindGroups}");

        State.EnsureBindGroupSlot(index);
        ref var binding = ref State.BindGroups[index];
        if (binding.BindGroup.NativeHandle() == bindGroup.NativeHandle() &&
            binding.DynamicOffsetCount == 1 &&
            binding.DynamicOffset == dynamicOffset) return;

        commands.SetBindGroup(slot, bindGroup, dynamicOffset);
        binding.BindGroup = bindGroup;
        binding.DynamicOffsetCount = 1;
        binding.DynamicOffset = dynamicOffset;
    }

    public void SetVertexBuffer(uint slot, WgpuBuffer buffer, ulong offset, ulong size)
    {
        var index = checked((int)slot);
        if (index >= backend.MaxVertexBuffers)
            throw new InvalidOperationException($"WebGPU vertex buffer slot {slot} exceeds device limit {backend.MaxVertexBuffers}");

        State.EnsureVertexBufferSlot(index);
        ref var binding = ref State.VertexBuffers[index];
        if (binding.Buffer.NativeHandle() == buffer.NativeHandle() && binding.Offset == offset && binding.Size == size) return;

        commands.SetVertexBuffer(slot, buffer, offset, size);
        binding.Buffer = buffer;
        binding.Offset = offset;
        binding.Size = size;
    }

    public void SetIndexBuffer(WgpuBuffer buffer, WGPUIndexFormat format, ulong offset, ulong size)
        => commands.SetIndexBuffer(buffer, format, offset, size);

    public void SetViewport(float x, float y, float width, float height, float minDepth = 0, float maxDepth = 1)
    {
        if (HasContents)
            commands.SetViewport(x, y, width, height, minDepth, maxDepth);
    }

    public void SetScissorRect(uint x, uint y, uint width, uint height)
    {
        if (HasContents)
            commands.SetScissorRect(x, y, width, height);
    }

    public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
        => commands.Draw(vertexCount, instanceCount, firstVertex, firstInstance);

    public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int baseVertex = 0, uint firstInstance = 0)
        => commands.DrawIndexed(indexCount, instanceCount, firstIndex, baseVertex, firstInstance);
}