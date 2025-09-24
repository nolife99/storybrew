namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Shaders;
using BrewLib.Util;
using OpenTK.Graphics.OpenGL;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

abstract class PrimitiveStreamerVao<TPrimitive> : IPrimitiveStreamer<TPrimitive> where TPrimitive : unmanaged
{
    protected static readonly int PrimitiveSize = Unsafe.SizeOf<TPrimitive>();
    readonly int commandSize;

    protected readonly int MaxPrimitivesPerBatch;

    readonly VertexDeclaration vertexDeclaration;
    bool Bound;

    ValueList<MultiDrawArraysIndirectCommand> commandBuffer;
    ValueList<MultiDrawElementsIndirectCommand> commandBufferIndexed;

    nint commandBufferMap;

    Shader currentShader;

    protected int totalQueuedPrimitives, commandBufferOffset, indexBufferId = -1, vertexBufferId = -1;
    int vertexArrayId = -1, commandBufferId = -1, commandBufferSize;

    protected PrimitiveStreamerVao(VertexDeclaration vertexDeclaration,
        int maxPrimitivesPerBatch,
        scoped ReadOnlySpan<ushort> indices)
    {
        if (vertexDeclaration.AttributeCount < 1)
            throw new ArgumentException("At least one vertex attribute is required");

        MaxPrimitivesPerBatch = maxPrimitivesPerBatch;
        this.vertexDeclaration = vertexDeclaration;

        commandSize = indices.IsEmpty ?
            Unsafe.SizeOf<MultiDrawArraysIndirectCommand>() :
            Unsafe.SizeOf<MultiDrawElementsIndirectCommand>();

        initializeDrawCommandBuffer(maxPrimitivesPerBatch);

        if (!indices.IsEmpty) initializeIndexBuffer(indices);
        else commandBuffer = ValueList.Create<MultiDrawArraysIndirectCommand>();
    }

    public GpuCommandSync FrameSync { get; } = new();

    public void AddPrimitive(scoped ref readonly TPrimitive primitive)
    {
        if (totalQueuedPrimitives == MaxPrimitivesPerBatch) DrawState.FlushRenderer(true);
        internalAddPrimitive(in primitive);

        ++PrimitivesInBatch;
        ++totalQueuedPrimitives;
    }

    public void Bind(Shader shader)
    {
        if (Bound || shader is null) return;

        if (currentShader != shader) setupVertexArray(shader);

        GL.BindBuffer(BufferTarget.DrawIndirectBuffer, commandBufferId);
        GL.BindVertexArray(vertexArrayId);

        internalBind();

        Bound = true;
    }

    public void Unbind()
    {
        if (!Bound) return;

        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.DrawIndirectBuffer, 0);

        Bound = false;
    }

    public void Render(PrimitiveType type, int vertexCount)
    {
        if (!Bound || totalQueuedPrimitives == 0) return;

        var span = indexBufferId != -1 ?
            MemoryMarshal.AsBytes(commandBufferIndexed.AsReadOnlySpan()) :
            MemoryMarshal.AsBytes(commandBuffer.AsReadOnlySpan());

        if (commandBufferOffset + span.Length > commandBufferSize) commandBufferOffset = 0;

        if (commandBufferMap != 0)
        {
            FrameSync.WaitAndLockRange(commandBufferId, commandBufferOffset, span.Length);
            span.CopyTo(commandBufferMap.AsSpan<byte>(commandBufferSize)[commandBufferOffset..]);

            GL.FlushMappedBufferRange(BufferTarget.DrawIndirectBuffer, commandBufferOffset, span.Length);
        }
        else
            GL.BufferSubData(BufferTarget.DrawIndirectBuffer,
                commandBufferOffset,
                span.Length,
                ref MemoryMarshal.GetReference(span));

        internalRender(type);

        FrameSync.CommitPending();

        commandBufferOffset += span.Length;

        if (indexBufferId != -1) commandBufferIndexed.Clear();
        else commandBuffer.Clear();

        totalQueuedPrimitives = 0;
    }

    public int QueuedRenders => indexBufferId != -1 ? commandBufferIndexed.Count : commandBuffer.Count;
    public int PrimitivesInBatch { get; private set; }

    public void QueueRender(int indexCount, int vertexCount)
    {
        var baseIndex = (totalQueuedPrimitives - PrimitivesInBatch) * vertexCount;
        internalQueueRender(ref baseIndex, vertexCount);

        if (indexBufferId != -1)
            commandBufferIndexed.Add(new()
            {
                Count = (uint)(PrimitivesInBatch * indexCount),
                InstanceCount = 1,
                FirstIndex = 0,
                BaseVertex = baseIndex,
                BaseInstance = 0
            });
        else
            commandBuffer.Add(new()
            {
                Count = (uint)(PrimitivesInBatch * indexCount),
                InstanceCount = 1,
                FirstVertex = (uint)baseIndex,
                BaseInstance = 0
            });

        PrimitivesInBatch = 0;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void internalBind() { }
    protected virtual void internalQueueRender(ref int baseIndex, int vertexCount) { }

    protected abstract void internalAddPrimitive(scoped ref readonly TPrimitive primitive);

    protected abstract void internalRender(PrimitiveType type);

    protected virtual void initializeVertexBuffer()
        => GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferId = GL.GenBuffer());

    void initializeIndexBuffer(scoped ReadOnlySpan<ushort> indices)
    {
        indexBufferId = GL.GenBuffer();

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBufferId);
        GL.BufferData(BufferTarget.ElementArrayBuffer,
            MemoryMarshal.AsBytes(indices).Length,
            ref MemoryMarshal.GetReference(indices),
            BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);

        commandBufferIndexed = ValueList.Create<MultiDrawElementsIndirectCommand>();
    }

    void initializeDrawCommandBuffer(int minRenderableVertexCount)
    {
        if (commandBufferId != -1) GL.DeleteBuffer(commandBufferId);
        commandBufferSize = minRenderableVertexCount * commandSize;

        GL.BindBuffer(BufferTarget.DrawIndirectBuffer, commandBufferId = GL.GenBuffer());
        if (DrawState.SupportsImmutable && DrawState.Extensions.Contains("GL_ARB_map_buffer_range"))
        {
            GL.BufferStorage(BufferTarget.DrawIndirectBuffer,
                commandBufferSize,
                0,
                BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit);

            commandBufferMap = GL.MapBufferRange(BufferTarget.DrawIndirectBuffer,
                0,
                commandBufferSize,
                MapBufferAccessMask.MapWriteBit | MapBufferAccessMask.MapPersistentBit |
                MapBufferAccessMask.MapFlushExplicitBit | MapBufferAccessMask.MapInvalidateBufferBit |
                MapBufferAccessMask.MapUnsynchronizedBit);
        }
        else GL.BufferData(BufferTarget.DrawIndirectBuffer, commandBufferSize, 0, BufferUsageHint.DynamicDraw);

        GL.BindBuffer(BufferTarget.DrawIndirectBuffer, 0);

        initializeVertexBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    void setupVertexArray(Shader shader)
    {
        var initial = currentShader is null;
        if (initial) vertexArrayId = GL.GenVertexArray();

        GL.BindVertexArray(vertexArrayId);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferId);

        if (!initial) vertexDeclaration.DeactivateAttributes(currentShader);
        vertexDeclaration.ActivateAttributes(shader);

        if (initial && indexBufferId != -1) GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBufferId);

        currentShader = shader;
    }

    ~PrimitiveStreamerVao() => Dispose(false);

    protected virtual void Dispose(bool disposing)
    {
        Unbind();

        GL.DeleteVertexArray(vertexArrayId);
        GL.DeleteBuffer(vertexBufferId);
        GL.DeleteBuffer(commandBufferId);

        if (indexBufferId != -1) GL.DeleteBuffer(indexBufferId);

        if (!disposing) return;

        commandBuffer.Dispose();
        commandBufferIndexed.Dispose();

        FrameSync.Dispose();
    }

    protected static bool HasCapabilities()
        => GpuCommandSync.HasCapabilities() && DrawState.Extensions.Contains("GL_ARB_vertex_array_object") &&
            DrawState.Extensions.Contains("GL_ARB_draw_indirect");
}