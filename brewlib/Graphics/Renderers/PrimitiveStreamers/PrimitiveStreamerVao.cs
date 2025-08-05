namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Shaders;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Internals;

abstract class PrimitiveStreamerVao<TPrimitive> : IPrimitiveStreamer<TPrimitive> where TPrimitive : struct
{
    readonly int commandSize;

    readonly VertexDeclaration vertexDeclaration;
    bool Bound;

    PooledList<byte> commandBuffer;

    protected int totalQueuedPrimitives, queuedRenders;
    int vertexArrayId = -1, commandBufferId = -1, commandBufferSize;

    protected PrimitiveStreamerVao(VertexDeclaration vertexDeclaration,
        int maxPrimitivesPerBatch,
        scoped ReadOnlySpan<ushort> indices)
    {
        if (vertexDeclaration.AttributeCount < 1) throw new ArgumentException("At least one vertex attribute is required");

        MaxPrimitivesPerBatch = maxPrimitivesPerBatch;
        this.vertexDeclaration = vertexDeclaration;
        PrimitiveSize = Unsafe.SizeOf<TPrimitive>();

        commandSize = indices.IsEmpty ?
            Unsafe.SizeOf<MultiDrawArraysIndirectCommand>() :
            Unsafe.SizeOf<MultiDrawElementsIndirectCommand>();

        initializeDrawCommandBuffer(maxPrimitivesPerBatch);

        if (!indices.IsEmpty) initializeIndexBuffer(indices);
    }

    protected Shader CurrentShader { get; set; }
    protected int VertexBufferId { get; private set; } = -1;
    protected int IndexBufferId { get; private set; } = -1;
    protected int PrimitiveSize { get; }
    protected int MaxPrimitivesPerBatch { get; set; }

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

        GL.BindVertexArray(0);
        Bound = false;
    }

    public void Render(PrimitiveType type, int vertexCount)
    {
        if (!Bound) return;

        GL.BufferSubData(BufferTarget.DrawIndirectBuffer,
            0,
            commandBuffer.Count,
            ref MemoryMarshal.GetReference(commandBuffer.AsReadOnlySpan()));

        internalRender(type, vertexCount);

        commandBuffer.Clear();

        queuedRenders = 0;
        totalQueuedPrimitives = 0;
    }

    public int QueuedRenders => queuedRenders;
    public int PrimitivesInBatch { get; private set; }

    public void QueueRender(int indexCount, int vertexCount)
    {
        var baseIndex = (totalQueuedPrimitives - PrimitivesInBatch) * vertexCount;
        internalQueueRender(ref baseIndex);

        ref var commandBytes = ref MemoryMarshal.GetReference(commandBuffer.GetAddSpan(commandSize));
        if (IndexBufferId != -1)
        {
            ref var command = ref Unsafe.As<byte, MultiDrawElementsIndirectCommand>(ref commandBytes);

            command.Count = (uint)(PrimitivesInBatch * indexCount);
            command.InstanceCount = 1;
            command.FirstIndex = 0;
            command.BaseVertex = baseIndex;
            command.BaseInstance = 0;
        }
        else
        {
            ref var command = ref Unsafe.As<byte, MultiDrawArraysIndirectCommand>(ref commandBytes);

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

        commandBufferId = GL.GenBuffer();
        commandBufferSize = minRenderableVertexCount * commandSize;

        commandBuffer = new(minRenderableVertexCount);

        GL.BindBuffer(BufferTarget.DrawIndirectBuffer, commandBufferId);
        GL.BufferStorage(BufferTarget.DrawIndirectBuffer, commandBufferSize, 0, BufferStorageFlags.DynamicStorageBit);

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

        if (disposing) commandBuffer.Dispose();
    }

    public static bool HasCapabilities() => GLFW.ExtensionSupported("GL_ARB_buffer_storage") &&
        GLFW.ExtensionSupported("GL_ARB_shader_storage_buffer_object") &&
        GLFW.ExtensionSupported("GL_ARB_multi_draw_indirect");
}