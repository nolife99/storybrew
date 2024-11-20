namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;

public class PrimitiveStreamerPersistentMap<TPrimitive>(VertexDeclaration vertexDeclaration,
    int minRenderableVertexCount,
    ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indices), PrimitiveStreamer
    where TPrimitive : unmanaged
{
    int bufferOffset, drawOffset, vertexBufferSize;
    nint bufferPointer;
    GpuCommandSync commandSync = new();

    public override unsafe void Render(PrimitiveType type, nint primitives, int count, int drawCount, bool canBuffer)
    {
        var vertexDataSize = count * PrimitiveSize;
        if (bufferOffset + vertexDataSize > vertexBufferSize)
        {
            bufferOffset = 0;
            drawOffset = 0;
        }

        if (commandSync.WaitForRange(bufferOffset, vertexDataSize))
        {
            ++BufferWaitCount;
            expandVertexBuffer();
        }

        NativeMemory.Copy((void*)primitives, (byte*)bufferPointer + bufferOffset, (nuint)vertexDataSize);

        if (IndexBufferId != -1) GL.DrawElements(type, drawCount, DrawElementsType.UnsignedShort, drawOffset * sizeof(ushort));
        else GL.DrawArrays(type, drawOffset, drawCount);

        commandSync.LockRange(bufferOffset, vertexDataSize);

        bufferOffset += vertexDataSize;
        drawOffset += drawCount;
    }
    protected override void initializeVertexBuffer()
    {
        base.initializeVertexBuffer();
        vertexBufferSize = MinRenderableVertexCount * VertexDeclaration.VertexSize;

        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);

        const int flags =
            (int)(BufferAccessMask.MapWriteBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapCoherentBit);

        GL.BufferStorage(BufferTarget.ArrayBuffer, vertexBufferSize, 0, (BufferStorageFlags)flags);
        bufferPointer = GL.MapBufferRange(BufferTarget.ArrayBuffer, 0, vertexBufferSize, (BufferAccessMask)flags);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }
    protected override void Dispose(bool disposing)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
        GL.UnmapBuffer(BufferTarget.ArrayBuffer);

        if (disposing)
        {
            commandSync.Dispose();
            commandSync = null;
        }

        base.Dispose(disposing);
    }
    void expandVertexBuffer()
    {
        // Prevent the vertex buffer from becoming too large (maxes at 4mb * grow factor)
        if (IndexBufferId != -1 || MinRenderableVertexCount * VertexDeclaration.VertexSize > 4194304) return;

        MinRenderableVertexCount = (int)(MinRenderableVertexCount * 1.75f);
        if (commandSync.WaitForAll()) ++BufferWaitCount;

        Unbind();

        // Rebuild the VBO

        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
        GL.UnmapBuffer(BufferTarget.ArrayBuffer);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.DeleteBuffer(VertexBufferId);

        initializeVertexBuffer();

        // Rebuild the VAO

        Bind(CurrentShader);
        CurrentShader = null;

        bufferOffset = 0;
        drawOffset = 0;

        ++DiscardedBufferCount;
    }

    public new static bool HasCapabilities() => DrawState.HasCapabilities(4, 4, "GL_ARB_buffer_storage") &&
        DrawState.HasCapabilities(3, 0, "GL_ARB_map_buffer_range") && DrawState.HasCapabilities(1, 5) &&
        GpuCommandSync.HasCapabilities() && PrimitiveStreamerVao<TPrimitive>.HasCapabilities();
}