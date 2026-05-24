namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Runtime.CompilerServices;
using Silk.NET.WebGPU;
using WgpuBuffer = Silk.NET.WebGPU.Buffer;

public unsafe sealed partial class WebGpuGraphicsBackend
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetReadyRenderPass(out RenderPassEncoder* readyRenderPass)
    {
        readyRenderPass = RenderPass;
        return readyRenderPass is not null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryRequireRenderPass(out RenderPassEncoder* readyRenderPass, bool waitForSurfaceTexture = true)
    {
        readyRenderPass = RenderPass;
        if (RenderPass is not null)
            return true;

        if (CommandEncoder is null)
            throw new InvalidOperationException("WebGPU render pass requested outside an active frame");

        if (frameTextureView is null && !tryPrepareFrameSurfaceTexture(waitForSurfaceTexture))
            return false;

        RenderPassColorAttachment colorAttachment = new()
        {
            View = frameTextureView,
            LoadOp = framebufferHasContents ? LoadOp.Load : LoadOp.Clear,
            StoreOp = StoreOp.Store,
            ClearValue = new()
            {
                R = frameClearColor.X,
                G = frameClearColor.Y,
                B = frameClearColor.Z,
                A = frameClearColor.W
            }
        };

        RenderPassDescriptor descriptor = new()
        {
            ColorAttachmentCount = 1,
            ColorAttachments = &colorAttachment
        };

        RenderPass = Api.CommandEncoderBeginRenderPass(CommandEncoder, in descriptor);
        if (RenderPass is null)
            throw new InvalidOperationException("Unable to begin WebGPU render pass");

        DrawState.CountDrawCall();

        ++RenderPassSerial;
        renderPassState.Reset();
        graphicsDevice.ApplyRenderPassState(RenderPass);
        readyRenderPass = RenderPass;
        return true;
    }

    internal void EndRenderPass()
    {
        if (RenderPass is null) return;

        Api.RenderPassEncoderEnd(RenderPass);
        Api.RenderPassEncoderRelease(RenderPass);
        RenderPass = null;
        framebufferHasContents = true;
    }

    internal void SetRenderPipeline(RenderPipeline* pipeline)
    {
        if (renderPassState.Pipeline == pipeline) return;

        Api.RenderPassEncoderSetPipeline(RenderPass, pipeline);
        renderPassState.Pipeline = pipeline;
    }

    internal void SetBindGroup(uint slot, BindGroup* bindGroup)
    {
        var index = checked((int)slot);
        renderPassState.EnsureBindGroupSlot(index);
        ref var binding = ref renderPassState.BindGroups[index];
        if (binding.BindGroup == bindGroup &&
            binding.DynamicOffsetCount == 0)
            return;

        Api.RenderPassEncoderSetBindGroup(RenderPass,
            slot,
            bindGroup,
            0,
            null);

        binding.BindGroup = bindGroup;
        binding.DynamicOffsetCount = 0;
        binding.DynamicOffset = 0;
    }

    internal void SetBindGroup(uint slot, BindGroup* bindGroup, uint dynamicOffset)
    {
        var index = checked((int)slot);
        renderPassState.EnsureBindGroupSlot(index);
        ref var binding = ref renderPassState.BindGroups[index];
        if (binding.BindGroup == bindGroup &&
            binding.DynamicOffsetCount == 1 &&
            binding.DynamicOffset == dynamicOffset)
            return;

        Api.RenderPassEncoderSetBindGroup(RenderPass,
            slot,
            bindGroup,
            1,
            &dynamicOffset);

        binding.BindGroup = bindGroup;
        binding.DynamicOffsetCount = 1;
        binding.DynamicOffset = dynamicOffset;
    }

    internal void SetVertexBuffer(uint slot, WgpuBuffer* buffer, ulong offset, ulong size)
    {
        var index = checked((int)slot);
        renderPassState.EnsureVertexBufferSlot(index);
        ref var binding = ref renderPassState.VertexBuffers[index];
        if (binding.Buffer == buffer &&
            binding.Offset == offset &&
            binding.Size == size)
            return;

        Api.RenderPassEncoderSetVertexBuffer(RenderPass, slot, buffer, offset, size);
        binding.Buffer = buffer;
        binding.Offset = offset;
        binding.Size = size;
    }
}