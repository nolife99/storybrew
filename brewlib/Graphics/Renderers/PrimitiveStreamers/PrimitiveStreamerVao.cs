namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shaders;

public abstract class PrimitiveStreamerVao<TPrimitive> : IPrimitiveStreamer<TPrimitive>
    where TPrimitive : struct, allows ref struct
{
    readonly int commandSize;
    bool Bound;

    nint commandsPtr, commandSync;
    protected int totalQueuedPrimitives, queuedRenders, commandPtrOffset;
    int vertexArrayId = -1, commandBufferId = -1, commandBufferSize;

    protected PrimitiveStreamerVao(VertexDeclaration vertexDeclaration,
        int minRenderableVertexCount,
        ReadOnlySpan<ushort> indices)
    {
        if (vertexDeclaration.AttributeCount < 1) throw new ArgumentException("At least one vertex attribute is required");

        MinRenderableVertexCount = minRenderableVertexCount;
        VertexDeclaration = vertexDeclaration;
        PrimitiveSize = Unsafe.SizeOf<TPrimitive>();

        commandSize = indices.IsEmpty ?
            Unsafe.SizeOf<MultiDrawArraysIndirectCommand>() :
            Unsafe.SizeOf<MultiDrawElementsIndirectCommand>();

        initializeVertexBuffer();
        initializeDrawCommandBuffer(minRenderableVertexCount);

        if (!indices.IsEmpty) initializeIndexBuffer(indices);
    }

    protected Shader CurrentShader { get; set; }
    protected int VertexBufferId { get; private set; } = -1;
    protected int IndexBufferId { get; private set; } = -1;
    protected int PrimitiveSize { get; }
    protected int MinRenderableVertexCount { get; set; }
    protected VertexDeclaration VertexDeclaration { get; }

    public void AddPrimitive(ref readonly TPrimitive primitive, int vertexCount)
    {
        if (totalQueuedPrimitives == MinRenderableVertexCount / vertexCount) DrawState.FlushRenderer(true);
        internalAddPrimitive(in primitive);

        ++PrimitivesInBatch;
        ++totalQueuedPrimitives;
    }

    public void Bind(Shader shader)
    {
        if (Bound || shader is null) return;

        if (CurrentShader != shader) setupVertexArray(shader);
        GL.BindVertexArray(vertexArrayId);

        GL.BindBuffer(BufferTarget.DrawIndirectBuffer, commandBufferId);

        internalBind();

        Bound = true;
    }

    public void Unbind()
    {
        if (!Bound) return;

        GL.BindVertexArray(0);
        Bound = false;
    }

    public void Render(PrimitiveType type, int vertexCount)
    {
        var dataSize = queuedRenders * commandSize;
        GL.FlushMappedBufferRange(BufferTarget.DrawIndirectBuffer, commandPtrOffset, dataSize);

        internalRender(type, vertexCount);
        commandSync = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);

        commandPtrOffset += dataSize;
        if (commandPtrOffset + MinRenderableVertexCount * commandSize > commandBufferSize) commandPtrOffset = 0;

        queuedRenders = 0;
        totalQueuedPrimitives = 0;
    }

    public int QueuedRenders => queuedRenders;
    public int PrimitivesInBatch { get; private set; }

    public void QueueRender(int indexCount, int vertexCount)
    {
        var baseIndex = (totalQueuedPrimitives - PrimitivesInBatch) * vertexCount;
        internalQueueRender(ref baseIndex);

        if (commandSync != 0)
        {
            GL.ClientWaitSync(commandSync, ClientWaitSyncFlags.SyncFlushCommandsBit, long.MaxValue);
            GL.DeleteSync(commandSync);

            commandSync = 0;
        }

        if (IndexBufferId != -1)
        {
            ref var command = ref Unsafe.Add(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<MultiDrawElementsIndirectCommand>(),
                    commandsPtr + commandPtrOffset),
                queuedRenders);

            command.Count = (uint)(PrimitivesInBatch * indexCount);
            command.InstanceCount = 1;
            command.FirstIndex = 0;
            command.BaseVertex = baseIndex;
            command.BaseInstance = 0;
        }
        else
        {
            ref var command = ref Unsafe.Add(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<MultiDrawArraysIndirectCommand>(),
                    commandsPtr + commandPtrOffset),
                queuedRenders);

            command.Count = (uint)(PrimitivesInBatch * indexCount);
            command.InstanceCount = 1;
            command.FirstVertex = (uint)baseIndex;
            command.BaseInstance = 0;
        }

        ++queuedRenders;
        PrimitivesInBatch = 0;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void internalBind() { }
    protected virtual void internalQueueRender(ref int baseIndex) { }

    protected abstract void internalAddPrimitive(ref readonly TPrimitive primitive);

    protected abstract void internalRender(PrimitiveType type, int vertexCount);

    protected virtual void initializeVertexBuffer()
        => GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId = GL.GenBuffer());

    void initializeIndexBuffer(ReadOnlySpan<ushort> indices)
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

        commandBufferId = GL.GenBuffer();
        commandBufferSize = minRenderableVertexCount * commandSize;

        GL.BindBuffer(BufferTarget.DrawIndirectBuffer, commandBufferId);
        GL.BufferStorage(BufferTarget.DrawIndirectBuffer,
            commandBufferSize,
            0,
            BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit);

        commandsPtr = GL.MapBufferRange(BufferTarget.DrawIndirectBuffer,
            0,
            commandBufferSize,
            MapBufferAccessMask.MapWriteBit |
            MapBufferAccessMask.MapUnsynchronizedBit |
            MapBufferAccessMask.MapPersistentBit |
            MapBufferAccessMask.MapInvalidateBufferBit |
            MapBufferAccessMask.MapFlushExplicitBit);
    }

    void setupVertexArray(Shader shader)
    {
        var initial = CurrentShader is null;
        if (initial) vertexArrayId = GL.GenVertexArray();

        GL.BindVertexArray(vertexArrayId);
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);

        if (!initial) VertexDeclaration.DeactivateAttributes(CurrentShader);
        VertexDeclaration.ActivateAttributes(shader);

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

        if (commandSync != 0) GL.DeleteSync(commandSync);
    }

    public static bool HasCapabilities() => GLFW.ExtensionSupported("GL_ARB_buffer_storage") &&
        GLFW.ExtensionSupported("GL_ARB_shader_storage_buffer_object") &&
        GLFW.ExtensionSupported("GL_ARB_multi_draw_indirect");
}