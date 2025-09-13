namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using BrewLib.Graphics.Shaders;
using BrewLib.Util;
using OpenTK.Graphics.OpenGL;

sealed class PrimitiveStreamerPersistentMap<TPrimitive>(VertexDeclaration vertexDeclaration,
    int maxPrimitivesPerBatch,
    scoped ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, maxPrimitivesPerBatch, indices) where TPrimitive : unmanaged
{
    nint bufferAddr;
    int bufferOffset, primitivesInCurrentRegion, vertexBufferSize;

    (int Start, int ClientStart) needsFlush;

    protected override void internalAddPrimitive(scoped ref readonly TPrimitive primitive)
    {
        var writePosition = bufferOffset + primitivesInCurrentRegion * PrimitiveSize;
        if (writePosition + PrimitiveSize > vertexBufferSize)
        {
            DrawState.FlushRenderer();
            needsFlush = (bufferOffset, primitivesInCurrentRegion);

            bufferOffset = 0;
            primitivesInCurrentRegion = 0;

            writePosition = 0;
        }

        FrameSync.WaitForRange(VertexBufferId, writePosition, PrimitiveSize);
        (bufferAddr + writePosition).AsRef<TPrimitive>() = primitive;

        ++primitivesInCurrentRegion;
    }

    protected override void internalQueueRender(ref int baseIndex, int vertexCount)
    {
        var primitiveStart = baseIndex / vertexCount;
        var vertexStride = PrimitiveSize / vertexCount;

        baseIndex = needsFlush.ClientStart != 0 && primitiveStart < needsFlush.ClientStart ?
            needsFlush.Start / vertexStride + primitiveStart * vertexCount :
            bufferOffset / vertexStride + (primitiveStart - needsFlush.ClientStart) * vertexCount;
    }

    protected override void internalRender(PrimitiveType type, int vertexCount)
    {
        var flushPreBytes = needsFlush.ClientStart * PrimitiveSize;
        var currentRegionBytes = primitivesInCurrentRegion * PrimitiveSize;

        if (flushPreBytes != 0)
        {
            FrameSync.LockRange(VertexBufferId, needsFlush.Start, flushPreBytes);
            GL.FlushMappedBufferRange(BufferTarget.ArrayBuffer, needsFlush.Start, flushPreBytes);
        }

        if (currentRegionBytes != 0)
        {
            FrameSync.LockRange(VertexBufferId, bufferOffset, currentRegionBytes);
            GL.FlushMappedBufferRange(BufferTarget.ArrayBuffer, bufferOffset, currentRegionBytes);
        }

        if (IndexBufferId != -1)
            GL.MultiDrawElementsIndirect(type, DrawElementsType.UnsignedShort, commandBufferOffset, QueuedRenders, 0);
        else GL.MultiDrawArraysIndirect(type, commandBufferOffset, QueuedRenders, 0);

        bufferOffset += currentRegionBytes;

        needsFlush = default;
        primitivesInCurrentRegion = 0;
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
}