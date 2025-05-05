namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Collections.Pooled;
using OpenTK.Graphics.OpenGL;
using Shaders;

public class PrimitiveStreamerPersistentMap<TPrimitive>(VertexDeclaration vertexDeclaration,
    int minRenderableVertexCount,
    ReadOnlySpan<ushort> indices) : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indices)
    where TPrimitive : struct, allows ref struct
{
    nint bufferAddr;
    int bufferOffset, vertexBufferSize, baseVertex;

    readonly PooledList<int> baseVertexList = new();

    protected override void internalQueueRender(ref int baseIndex) => baseVertexList.Add(baseVertex);

    protected override void internalAddPrimitive(ref readonly TPrimitive primitive)
    {
        if (GpuCommandSync.WaitForRange(bufferOffset, PrimitiveSize)) expandVertexBuffer();
        Unsafe.Add(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<TPrimitive>(), bufferAddr + bufferOffset),
            totalQueuedPrimitives) = primitive;
    }

    protected override void internalRender(PrimitiveType type, int vertexCount,
        ReadOnlySpan<int> counts,
        ReadOnlySpan<nint> indices,
        ReadOnlySpan<int> firsts)
    {
        var vertexDataSize = totalQueuedPrimitives * PrimitiveSize;
        GL.FlushMappedBufferRange(BufferTarget.ArrayBuffer, bufferOffset, vertexDataSize);

        if (IndexBufferId != -1)
            GL.MultiDrawElementsBaseVertex(type,
                ref MemoryMarshal.GetReference(counts),
                DrawElementsType.UnsignedShort,
                ref MemoryMarshal.GetReference(indices),
                counts.Length, ref MemoryMarshal.GetReference(baseVertexList.Span));
        else
            GL.MultiDrawArrays(type,
                ref MemoryMarshal.GetReference(firsts),
                ref MemoryMarshal.GetReference(counts),
                counts.Length);

        GpuCommandSync.LockRange(bufferOffset, vertexDataSize);

        if (bufferOffset + MinRenderableVertexCount * PrimitiveSize > vertexBufferSize)
        {
            bufferOffset = 0;
            baseVertex = 0;
            baseVertexList.Clear();
        }
        else
        {
            bufferOffset += vertexDataSize;
            baseVertex += totalQueuedPrimitives;
        }
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
            MapBufferAccessMask.MapUnsynchronizedBit |
            MapBufferAccessMask.MapInvalidateBufferBit);
    }

    protected override void Dispose(bool disposing)
    {
        GpuCommandSync.DeleteFences();
        base.Dispose(disposing);
    }

    void expandVertexBuffer()
    {
        // Prevent the vertex buffer from becoming too large (maxes at 4mb * grow factor)
        if (IndexBufferId != -1 || MinRenderableVertexCount * PrimitiveSize > 4194304) return;

        MinRenderableVertexCount = (int)(MinRenderableVertexCount * 1.5f);
        Trace.WriteLine($"[OpenGL] Expanding vertex buffer to {MinRenderableVertexCount * PrimitiveSize} bytes");

        GpuCommandSync.WaitForAll();

        Unbind();

        GL.DeleteBuffer(VertexBufferId);

        initializeVertexBuffer();

        Bind(CurrentShader);
        CurrentShader = null;

        bufferOffset = 0;
    }
}