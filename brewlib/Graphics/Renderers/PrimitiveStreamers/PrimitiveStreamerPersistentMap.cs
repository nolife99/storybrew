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
    MapBufferAccessMask accessMask;
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

        FrameSync.WaitForRange(vertexBufferId, writePosition, PrimitiveSize);
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

    protected override void internalRender(PrimitiveType type)
    {
        var flushPreBytes = needsFlush.ClientStart * PrimitiveSize;
        var currentRegionBytes = primitivesInCurrentRegion * PrimitiveSize;

        if (flushPreBytes != 0)
        {
            FrameSync.LockRange(vertexBufferId, needsFlush.Start, flushPreBytes);
            GL.FlushMappedBufferRange(BufferTarget.ArrayBuffer, needsFlush.Start, flushPreBytes);
        }

        if (currentRegionBytes != 0)
        {
            FrameSync.LockRange(vertexBufferId, bufferOffset, currentRegionBytes);
            GL.FlushMappedBufferRange(BufferTarget.ArrayBuffer, bufferOffset, currentRegionBytes);
        }

        var intermittent = (accessMask & MapBufferAccessMask.MapPersistentBit) == 0;
        if (intermittent) GL.UnmapBuffer(BufferTarget.ArrayBuffer);

        if (indexBufferId != -1)
            GL.MultiDrawElementsIndirect(type, DrawElementsType.UnsignedShort, commandBufferOffset, QueuedRenders, 0);
        else GL.MultiDrawArraysIndirect(type, commandBufferOffset, QueuedRenders, 0);

        bufferOffset += currentRegionBytes;

        if (intermittent) bufferAddr = GL.MapBufferRange(BufferTarget.ArrayBuffer, 0, vertexBufferSize, accessMask);

        needsFlush = default;
        primitivesInCurrentRegion = 0;
    }

    protected override void initializeVertexBuffer()
    {
        base.initializeVertexBuffer();
        vertexBufferSize = MaxPrimitivesPerBatch * PrimitiveSize;

        accessMask = MapBufferAccessMask.MapWriteBit | MapBufferAccessMask.MapFlushExplicitBit |
            MapBufferAccessMask.MapInvalidateBufferBit | MapBufferAccessMask.MapUnsynchronizedBit;

        if (DrawState.SupportsImmutable)
        {
            GL.BufferStorage(BufferTarget.ArrayBuffer,
                vertexBufferSize,
                0,
                BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit);

            accessMask |= MapBufferAccessMask.MapPersistentBit;
        }
        else GL.BufferData(BufferTarget.ArrayBuffer, vertexBufferSize, 0, BufferUsageHint.DynamicDraw);

        bufferAddr = GL.MapBufferRange(BufferTarget.ArrayBuffer, 0, vertexBufferSize, accessMask);
    }

    public new static bool HasCapabilities()
        => PrimitiveStreamerVao<TPrimitive>.HasCapabilities() &&
            DrawState.Extensions.Contains("GL_ARB_multi_draw_indirect");
}