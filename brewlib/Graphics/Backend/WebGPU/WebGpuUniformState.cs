namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Runtime.CompilerServices;
using Ahjo.Wgpu;

/// <summary>
///     Holds the uniform-upload strategy for a pipeline. Two strategies, chosen at pipeline-construction time:
///     <list type="bullet">
///         <item>
///             <b>Push constants</b> (when the device exposes the <c>Immediates</c> native feature and the payload
///             fits in <c>maxImmediateSize</c>): the value is staged in a small CPU buffer on each <c>SetValue</c>; the
///             bytes are then captured per draw and emitted with <c>SetPushConstants</c> during render-pass replay.
///         </item>
///         <item>
///             <b>Dynamic-offset uniform buffer</b> (fallback): a single uniform buffer is sub-allocated per draw in
///             a per-frame ring at the device's minimum uniform offset alignment. <c>StageDynamic</c> writes the slot
///             with a queue-ordered <c>WriteBuffer</c> and returns the bind group
///             plus dynamic offset, which the draw records and the replay binds.
///         </item>
///     </list>
///     All GPU work for a frame is recorded first and replayed inside a single render pass at EndFrame; the
///     dynamic-uniform writes here are queue operations, ordered before that frame's submit.
/// </summary>
sealed class WebGpuUniformState : IDisposable
{
    public enum Strategy
    {
        None,
        PushConstants,
        DynamicOffsetBuffer
    }

    readonly WebGpuDeviceContext deviceContext;
    readonly byte[] dynamicStaging;

    // Push-constant path: a single CPU staging area, refreshed on every SetValue.
    readonly byte[] pushConstantStaging;

    int frameWriteCursor;

    // Dynamic-offset path:
    WebGpuGraphicsBuffer uniformBuffer;

    public WebGpuUniformState(WebGpuDeviceContext deviceContext, Strategy strategy, uint payloadSize)
    {
        this.deviceContext = deviceContext;
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

    /// <summary>The currently-staged value as raw bytes (length == <see cref="PayloadSize" />). Push path only.</summary>
    public ReadOnlySpan<byte> CurrentValueBytes => pushConstantStaging.AsSpan(0, (int)PayloadSize);

    public void Dispose()
    {
        // We don't own the uniform buffer — the pipeline does.
    }

    /// <summary>Attach the dynamic-offset uniform buffer (created by the factory after the cache is in place).</summary>
    public void AttachUniformBuffer(WebGpuGraphicsBuffer buffer)
    {
        if (ActiveStrategy != Strategy.DynamicOffsetBuffer)
            throw new InvalidOperationException("Uniform buffer attach is only valid for the dynamic-offset strategy");

        uniformBuffer = buffer;
    }

    /// <summary>Reset per-frame ring state. Called at frame start.</summary>
    public void BeginFrame()
    {
        frameWriteCursor = 0;
        HasValueWritten = false;
    }

    /// <summary>
    ///     Copy a value (already serialised to raw bytes) into the staging area. The size must equal
    ///     <see cref="PayloadSize" />. The next draw will capture / upload it.
    /// </summary>
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

    /// <summary>
    ///     Dynamic-offset path only: write the current value into the per-frame uniform ring with a queue-ordered
    ///     <c>WriteBuffer</c> and return the bind group + dynamic offset for the draw to record.
    /// </summary>
    public (BindGroup Group, uint Offset) StageDynamic(WebGpuBindGroupCache cache)
    {
        if (ActiveStrategy != Strategy.DynamicOffsetBuffer)
            throw new InvalidOperationException("StageDynamic is only valid for the dynamic-offset strategy");

        if (uniformBuffer is null)
            throw new InvalidOperationException("Uniform buffer hasn't been attached");

        var offset = (uint)frameWriteCursor;
        var nextCursor = (int)(offset + AlignedSlotSize);
        EnsureRingCapacity(nextCursor);

        // The buffer's underlying allocation may have changed (grow) — Resized events already invalidated any
        // dependent bind groups, so this returns a fresh/valid group.
        var group = cache.GetUniformBindGroup(uniformBuffer, AlignedSlotSize, true);

        // Queue.WriteBuffer is queue-ordered before the next submit, so the slot is visible to this frame's draws.
        deviceContext.Queue.WriteBuffer(uniformBuffer.Buffer, offset, dynamicStaging.AsSpan(0, (int)AlignedSlotSize));

        frameWriteCursor = nextCursor;
        return (group, offset);
    }

    void EnsureRingCapacity(int requiredBytes)
    {
        if (uniformBuffer.CapacityBytes >= requiredBytes) return;

        var slots = Math.Max(8, requiredBytes / (int)AlignedSlotSize + 1);
        uniformBuffer.Allocate(slots * (int)AlignedSlotSize);
    }
}

/// <summary>
///     Concrete <see cref="IRenderUniform{T}" />. Forwards to the pipeline's shared <see cref="WebGpuUniformState" />.
///     The <c>IRenderUniform&lt;T&gt;</c> interface places no <c>unmanaged</c> constraint on <typeparamref name="T" />,
///     so we validate at construction that it's blittable and serialise with <see cref="Unsafe.WriteUnaligned" />
///     (a safe managed API — no pointers, no <c>unsafe</c> blocks).
/// </summary>
sealed class WebGpuRenderUniform<T> : IRenderUniform<T>
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
        var size = Unsafe.SizeOf<T>();
        var tmp = size <= 256 ? stackalloc byte[size] : new byte[size];
        Unsafe.WriteUnaligned(ref tmp[0], value);
        state.SetValueBytes(tmp);
    }
}