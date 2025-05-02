namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using Shaders;
using Util;

public class PrimitiveStreamerPersistentMap<TPrimitive>(VertexDeclaration vertexDeclaration,
    int minRenderableVertexCount,
    ReadOnlySpan<ushort> indices) : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indices)
    where TPrimitive : struct, allows ref struct
{
    nint bufferAddr, primitives;
    int bufferOffset, vertexBufferSize;

    protected override void internalBind() { }

    protected override void AddPrimitiveInternal(ref readonly TPrimitive primitive)
        => Unsafe.Add(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<TPrimitive>(), primitives), totalQueuedPrimitives) =
            primitive;

    protected override void RenderInternal(PrimitiveType type,
        ReadOnlySpan<int> counts,
        ReadOnlySpan<nint> indices,
        ReadOnlySpan<int> firsts)
    {
        var vertexDataSize = totalQueuedPrimitives * PrimitiveSize;
        if (bufferOffset + vertexDataSize > vertexBufferSize) bufferOffset = 0;

        if (GpuCommandSync.WaitForRange(bufferOffset, vertexDataSize)) expandVertexBuffer();

        Unsafe.CopyBlock(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), bufferAddr + bufferOffset),
            ref Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), primitives),
            (uint)vertexDataSize);

        GL.FlushMappedBufferRange(BufferTarget.ArrayBuffer, bufferOffset, vertexDataSize);

        // TODO: FIX THIS!!

        if (IndexBufferId != -1)
            GL.MultiDrawElements(type,
                ref MemoryMarshal.GetReference(counts),
                DrawElementsType.UnsignedShort,
                ref MemoryMarshal.GetReference(indices),
                counts.Length);
        else
            GL.MultiDrawArrays(type,
                ref MemoryMarshal.GetReference(firsts),
                ref MemoryMarshal.GetReference(counts),
                counts.Length);

        GpuCommandSync.LockRange(bufferOffset, vertexDataSize);

        bufferOffset += vertexDataSize;
    }

    protected override void initializeVertexBuffer()
    {
        base.initializeVertexBuffer();
        vertexBufferSize = MinRenderableVertexCount * VertexDeclaration.VertexSize;

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

        primitives = Native.AllocateMemory(vertexBufferSize);
    }

    protected override void Dispose(bool disposing)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
        GL.UnmapBuffer(BufferTarget.ArrayBuffer);

        Native.FreeMemory(primitives);

        GpuCommandSync.DeleteFences();
        base.Dispose(disposing);
    }

    void expandVertexBuffer()
    {
        // Prevent the vertex buffer from becoming too large (maxes at 4mb * grow factor)
        if (IndexBufferId != -1 || MinRenderableVertexCount * VertexDeclaration.VertexSize > 4194304) return;

        MinRenderableVertexCount = (int)(MinRenderableVertexCount * 1.75f);
        GpuCommandSync.WaitForAll();

        Unbind();

        GL.DeleteBuffer(VertexBufferId);

        initializeVertexBuffer();

        Bind(CurrentShader);
        CurrentShader = null;

        bufferOffset = 0;
    }
}