namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTK.Graphics.OpenGL;
using Shaders;

public class PrimitiveStreamerPersistentMap<TPrimitive> : PrimitiveStreamerVao<TPrimitive>
    where TPrimitive : struct, allows ref struct
{
    readonly int maxBatchSize;

    readonly GpuCommandSync sync = new();
    nint bufferAddr;
    int bufferOffset, vertexBufferSize, baseVertex;

    public PrimitiveStreamerPersistentMap(VertexDeclaration vertexDeclaration,
        int minRenderableVertexCount,
        ReadOnlySpan<ushort> indices) : base(vertexDeclaration, minRenderableVertexCount, indices)
        => maxBatchSize = minRenderableVertexCount * PrimitiveSize;

    protected override void internalQueueRender(ref int baseIndex) => baseIndex += baseVertex;

    protected override void internalAddPrimitive(ref readonly TPrimitive primitive)
    {
        if (sync.WaitForRange(bufferOffset, PrimitiveSize)) expandVertexBuffer();
        Unsafe.Add(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<TPrimitive>(), bufferAddr + bufferOffset),
            totalQueuedPrimitives) = primitive;
    }

    protected override void internalRender(PrimitiveType type, int vertexCount)
    {
        var vertexDataSize = totalQueuedPrimitives * PrimitiveSize;
        GL.FlushMappedBufferRange(BufferTarget.ArrayBuffer, bufferOffset, vertexDataSize);

        if (IndexBufferId != -1)
            GL.MultiDrawElementsIndirect(type, DrawElementsType.UnsignedShort, commandPtrOffset, queuedRenders, 0);
        else GL.MultiDrawArraysIndirect(type, commandPtrOffset, queuedRenders, 0);

        sync.LockRange(bufferOffset, vertexDataSize);

        bufferOffset += vertexDataSize;
        if (bufferOffset + maxBatchSize > vertexBufferSize)
        {
            bufferOffset = 0;
            baseVertex = 0;
        }
        else baseVertex += totalQueuedPrimitives * vertexCount;
    }

    protected override void initializeVertexBuffer()
    {
        base.initializeVertexBuffer();
        vertexBufferSize = MinRenderableVertexCount * PrimitiveSize;

        GL.BufferStorage(BufferTarget.ArrayBuffer,
            vertexBufferSize,
            0,
            BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit);

        bufferAddr = GL.MapBufferRange(BufferTarget.ArrayBuffer,
            0,
            vertexBufferSize,
            MapBufferAccessMask.MapWriteBit |
            MapBufferAccessMask.MapPersistentBit |
            MapBufferAccessMask.MapFlushExplicitBit |
            MapBufferAccessMask.MapUnsynchronizedBit);
    }

    protected override void Dispose(bool disposing)
    {
        sync.Dispose();
        base.Dispose(disposing);
    }

    void expandVertexBuffer()
    {
        // Prevent the vertex buffer from becoming too large (maxes at 4mb * grow factor)
        if (MinRenderableVertexCount * PrimitiveSize > 4194304) return;

        MinRenderableVertexCount = (int)(MinRenderableVertexCount * 1.5f);
        Trace.WriteLine($"[OpenGL] Expanding vertex buffer to {MinRenderableVertexCount * PrimitiveSize} bytes");

        sync.WaitForAll();

        Unbind();

        GL.DeleteBuffer(VertexBufferId);

        initializeVertexBuffer();

        Bind(CurrentShader);
        CurrentShader = null;

        bufferOffset = 0;
    }
}