namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;

public class PrimitiveStreamerPersistentMap<TPrimitive>(VertexDeclaration vertexDeclaration,
    int minRenderableVertexCount,
    ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indices) where TPrimitive : unmanaged
{
    readonly GpuCommandSync commandSync = new();
    nint bufferAddr;
    int bufferOffset, drawOffset, vertexBufferSize;

    public override void Render(PrimitiveType type, ReadOnlySpan<TPrimitive> primitives, int vertices)
    {
        var vertexDataSize = primitives.Length * PrimitiveSize;
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

        primitives.CopyTo(MemoryMarshal.CreateSpan(
            ref Unsafe.AddByteOffset(ref Unsafe.NullRef<TPrimitive>(), bufferAddr + bufferOffset), primitives.Length));

        var drawCount = primitives.Length * vertices;
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

        GL.NamedBufferStorage(VertexBufferId, vertexBufferSize, 0,
            BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit);

        bufferAddr = GL.MapNamedBufferRange(VertexBufferId, 0, vertexBufferSize,
            BufferAccessMask.MapWriteBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapCoherentBit |
            BufferAccessMask.MapUnsynchronizedBit | BufferAccessMask.MapInvalidateBufferBit);
    }
    protected override void Dispose(bool disposing)
    {
        GL.UnmapNamedBuffer(VertexBufferId);

        if (disposing) commandSync.Dispose();
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

        GL.UnmapNamedBuffer(VertexBufferId);
        GL.DeleteBuffer(VertexBufferId);

        initializeVertexBuffer();

        // Rebuild the VAO

        Bind(CurrentShader);
        CurrentShader = null;

        bufferOffset = 0;
        drawOffset = 0;

        ++DiscardedBufferCount;
    }

    public new static bool HasCapabilities() => GLFW.ExtensionSupported("GL_ARB_buffer_storage") &&
        GLFW.ExtensionSupported("GL_ARB_map_buffer_range") && GpuCommandSync.HasCapabilities() &&
        PrimitiveStreamerVao<TPrimitive>.HasCapabilities();
}