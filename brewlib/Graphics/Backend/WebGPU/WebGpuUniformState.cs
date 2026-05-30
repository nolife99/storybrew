namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Wgpu;

sealed class WebGpuUniformState
{
    public enum Strategy
    {
        None,
        PushConstants,
        DynamicOffsetBuffer
    }

    readonly byte[] dynamicStaging;
    readonly byte[] pushConstantStaging;

    public WebGpuUniformState(WebGpuDeviceContext deviceContext, Strategy strategy, uint payloadSize)
    {
        ActiveStrategy = strategy;
        PayloadSize = payloadSize;

        switch (strategy)
        {
            case Strategy.None:
                AlignedSlotSize = 0;
                break;

            case Strategy.PushConstants:
                AlignedSlotSize = (uint)WgpuMapper.AlignUp((int)payloadSize, 4);
                pushConstantStaging = new byte[AlignedSlotSize];
                break;

            case Strategy.DynamicOffsetBuffer:
                AlignedSlotSize = (uint)WgpuMapper.AlignUp(payloadSize, deviceContext.MinUniformOffsetAlignment);
                dynamicStaging = new byte[AlignedSlotSize];
                break;
        }
    }

    public Strategy ActiveStrategy { get; }
    public uint PayloadSize { get; }
    public uint AlignedSlotSize { get; }
    public bool HasValueWritten { get; private set; }

    public ReadOnlySpan<byte> CurrentValueBytes => pushConstantStaging.AsSpan(0, (int)PayloadSize);

    public void BeginFrame()
    {
        HasValueWritten = false;
    }

    public void SetValueBytes(scoped ReadOnlySpan<byte> bytes)
    {
        if ((uint)bytes.Length != PayloadSize)
            throw new ArgumentException(
                $"Uniform value size {bytes.Length} does not match pipeline's expected size {PayloadSize}",
                nameof(bytes));

        switch (ActiveStrategy)
        {
            case Strategy.None:
                throw new InvalidOperationException("This pipeline has no uniform — SetValue is invalid");

            case Strategy.PushConstants:
                bytes.CopyTo(pushConstantStaging);
                if ((uint)pushConstantStaging.Length > PayloadSize)
                    pushConstantStaging.AsSpan((int)PayloadSize).Clear();

                break;

            case Strategy.DynamicOffsetBuffer:
                bytes.CopyTo(dynamicStaging);
                if ((uint)dynamicStaging.Length > PayloadSize)
                    dynamicStaging.AsSpan((int)PayloadSize).Clear();

                break;
        }

        HasValueWritten = true;
    }

    public (BindGroup Group, uint Offset) StageDynamic(WebGpuBackend backend, WebGpuBindGroupCache cache)
    {
        if (ActiveStrategy != Strategy.DynamicOffsetBuffer)
            throw new InvalidOperationException("StageDynamic is only valid for the dynamic-offset strategy");

        var (buffer, offset) = backend.UniformRing.Allocate(AlignedSlotSize);
        var group = cache.GetUniformBindGroup(buffer, AlignedSlotSize, true);
        backend.StageBufferWrite(buffer.Buffer, offset, dynamicStaging.AsSpan(0, (int)AlignedSlotSize));

        return (group, offset);
    }
}

sealed class WebGpuRenderUniform<T> : IRenderUniform<T> where T : struct
{
    readonly WebGpuUniformState state;

    public WebGpuRenderUniform(WebGpuUniformState state)
    {
        this.state = state;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            throw new NotSupportedException($"Uniform type {typeof(T)} must be an unmanaged value type");

        if ((uint)Unsafe.SizeOf<T>() != state.PayloadSize)
            throw new ArgumentException(
                $"Uniform type {typeof(T).Name} has size {Unsafe.SizeOf<T>()} but pipeline expects {state.PayloadSize}");
    }

    public void SetValue(T value)
    {
        state.SetValueBytes(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1)));
    }
}