namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using BrewLib.Graphics.Shaders;
using BrewLib.Util;
using OpenTK.Graphics.OpenGL;

sealed class PrimitiveStreamerPersistentMap<TPrimitive>(VertexDeclaration vertexDeclaration,
    int maxPrimitivesPerBatch,
    scoped ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, maxPrimitivesPerBatch, indices) where TPrimitive : unmanaged
{
    nint bufferAddr;
    int bufferOffset, vertexBufferSize, baseVertex;

    (int Start, int ClientStart) needsFlush;

    protected override void internalQueueRender(ref int baseIndex) => baseIndex += baseVertex;

    protected override void internalAddPrimitive(scoped ref readonly TPrimitive primitive)
    {
        var writePosition = bufferOffset + totalQueuedPrimitives * PrimitiveSize;
        if (writePosition == vertexBufferSize)
        {
            needsFlush = (bufferOffset, totalQueuedPrimitives);

            bufferOffset = 0;
            baseVertex = 0;
        }

        FrameSync.WaitForRange(VertexBufferId, writePosition, PrimitiveSize);
        Unsafe.Add(ref (bufferAddr + bufferOffset).AsRef<TPrimitive>(), totalQueuedPrimitives) = primitive;
    }

    protected override void internalRender(PrimitiveType type, int vertexCount)
    {
        var flushVertexSize = needsFlush.ClientStart * PrimitiveSize;
        var vertexDataSize = totalQueuedPrimitives * PrimitiveSize;

        if (flushVertexSize != 0)
        {
            FrameSync.WaitAndLockRange(VertexBufferId, needsFlush.Start, flushVertexSize);

            Unsafe.CopyBlock(ref (bufferAddr + bufferOffset).AsRef<byte>(),
                ref (bufferAddr + needsFlush.Start).AsRef<byte>(),
                (uint)flushVertexSize);

            GL.FlushMappedBufferRange(BufferTarget.ArrayBuffer, needsFlush.Start, flushVertexSize);
        }

        if (vertexDataSize != 0)
        {
            FrameSync.LockRange(VertexBufferId, bufferOffset, vertexDataSize);
            GL.FlushMappedBufferRange(BufferTarget.ArrayBuffer, bufferOffset, vertexDataSize);
        }

        if (IndexBufferId != -1)
            GL.MultiDrawElementsIndirect(type, DrawElementsType.UnsignedShort, commandBufferOffset, queuedRenders, 0);
        else GL.MultiDrawArraysIndirect(type, commandBufferOffset, queuedRenders, 0);

        needsFlush = default;

        bufferOffset += vertexDataSize;
        baseVertex += totalQueuedPrimitives * vertexCount;
    }

    protected override void initializeVertexBuffer()
    {
        base.initializeVertexBuffer();
        vertexBufferSize = MaxPrimitivesPerBatch * PrimitiveSize;

        GL.BufferStorage(BufferTarget.ArrayBuffer,
            vertexBufferSize,
            0,
            BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit);

        bufferAddr = GL.MapBufferRange(BufferTarget.ArrayBuffer,
            0,
            vertexBufferSize,
            MapBufferAccessMask.MapWriteBit | MapBufferAccessMask.MapPersistentBit |
            MapBufferAccessMask.MapFlushExplicitBit | MapBufferAccessMask.MapInvalidateBufferBit |
            MapBufferAccessMask.MapUnsynchronizedBit);
    }

    protected override void internalBind() => GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);

    public new static bool HasCapabilities()
        => DrawState.Extensions.Contains("GL_ARB_multi_draw_indirect") &&
            DrawState.Extensions.Contains("GL_ARB_buffer_storage");
}