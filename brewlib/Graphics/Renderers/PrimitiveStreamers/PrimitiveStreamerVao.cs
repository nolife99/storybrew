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

    protected int totalQueuedPrimitives, commandBufferOffset;
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

        if (!indices.IsEmpty)
        {
            initializeIndexBuffer(indices);
            commandBufferIndexed = ValueList.Create<MultiDrawElementsIndirectCommand>();
        }
        else commandBuffer = ValueList.Create<MultiDrawArraysIndirectCommand>();
    }

    protected Shader CurrentShader { get; set; }
    protected int VertexBufferId { get; private set; } = -1;
    protected int IndexBufferId { get; private set; } = -1;

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

        if (CurrentShader != shader) setupVertexArray(shader);

        GL.BindBuffer(BufferTarget.DrawIndirectBuffer, commandBufferId);
        GL.BindVertexArray(vertexArrayId);

        internalBind();

        Bound = true;
    }

    public void Unbind()
    {
        if (!Bound) return;

        Bound = false;
        GL.BindVertexArray(0);
    }

    public void Render(PrimitiveType type, int vertexCount)
    {
        if (!Bound || totalQueuedPrimitives == 0) return;

        var span = IndexBufferId != -1 ?
            MemoryMarshal.AsBytes(commandBufferIndexed.AsReadOnlySpan()) :
            MemoryMarshal.AsBytes(commandBuffer.AsReadOnlySpan());

        if (commandBufferOffset + span.Length >= commandBufferSize) commandBufferOffset = 0;

        FrameSync.WaitAndLockRange(commandBufferId, commandBufferOffset, span.Length);
        span.CopyTo((commandBufferMap + commandBufferOffset).AsSpan<byte>(commandBufferSize));

        internalRender(type, vertexCount);

        FrameSync.CommitPending();

        commandBufferOffset += span.Length;

        if (IndexBufferId != -1) commandBufferIndexed.Clear();
        else commandBuffer.Clear();

        totalQueuedPrimitives = 0;
    }

    public int QueuedRenders => IndexBufferId != -1 ? commandBufferIndexed.Count : commandBuffer.Count;
    public int PrimitivesInBatch { get; private set; }

    public void QueueRender(int indexCount, int vertexCount)
    {
        var baseIndex = (totalQueuedPrimitives - PrimitivesInBatch) * vertexCount;
        internalQueueRender(ref baseIndex, vertexCount);

        if (IndexBufferId != -1)
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

    protected abstract void internalRender(PrimitiveType type, int vertexCount);

    protected virtual void initializeVertexBuffer()
        => GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId = GL.GenBuffer());

    void initializeIndexBuffer(scoped ReadOnlySpan<ushort> indices)
    {
        IndexBufferId = GL.GenBuffer();

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, IndexBufferId);
        GL.BufferStorage(BufferTarget.ElementArrayBuffer,
            indices.Length * sizeof(ushort),
            ref MemoryMarshal.GetReference(indices),
            BufferStorageFlags.None);
    }

    void initializeDrawCommandBuffer(int minRenderableVertexCount)
    {
        if (commandBufferId != -1) GL.DeleteBuffer(commandBufferId);

        GL.BindBuffer(BufferTarget.DrawIndirectBuffer, commandBufferId = GL.GenBuffer());
        GL.BufferStorage(BufferTarget.DrawIndirectBuffer,
            commandBufferSize = minRenderableVertexCount * commandSize,
            0,
            BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit);

        commandBufferMap = GL.MapBufferRange(BufferTarget.DrawIndirectBuffer,
            0,
            commandBufferSize,
            MapBufferAccessMask.MapWriteBit | MapBufferAccessMask.MapPersistentBit |
            MapBufferAccessMask.MapFlushExplicitBit | MapBufferAccessMask.MapInvalidateBufferBit |
            MapBufferAccessMask.MapUnsynchronizedBit);

        initializeVertexBuffer();
    }

    void setupVertexArray(Shader shader)
    {
        var initial = CurrentShader is null;
        if (initial) vertexArrayId = GL.GenVertexArray();

        GL.BindVertexArray(vertexArrayId);
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);

        if (!initial) vertexDeclaration.DeactivateAttributes(CurrentShader);
        vertexDeclaration.ActivateAttributes(shader);

        if (initial && IndexBufferId != -1) GL.BindBuffer(BufferTarget.ElementArrayBuffer, IndexBufferId);

        CurrentShader = shader;
    }

    ~PrimitiveStreamerVao() => Dispose(false);

    protected virtual void Dispose(bool disposing)
    {
        Unbind();

        GL.DeleteVertexArray(vertexArrayId);
        GL.DeleteBuffer(VertexBufferId);
        GL.DeleteBuffer(commandBufferId);

        if (IndexBufferId != -1) GL.DeleteBuffer(IndexBufferId);

        if (!disposing) return;

        commandBuffer.Dispose();
        commandBufferIndexed.Dispose();

        FrameSync.Dispose();
    }

    public static bool HasCapabilities()
        => DrawState.Extensions.Contains("GL_ARB_draw_indirect") &&
            DrawState.Extensions.Contains("GL_ARB_buffer_storage") &&
            DrawState.Extensions.Contains("GL_ARB_multi_draw_indirect");
}