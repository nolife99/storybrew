namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;

public class PrimitiveStreamerPersistentMap<TPrimitive>(VertexDeclaration vertexDeclaration,
    int minRenderableVertexCount,
    ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indices) where TPrimitive : allows ref struct
{
    nint bufferAddr, primitives;
    int bufferOffset, drawOffset, vertexBufferSize;

    public override ref TPrimitive PrimitiveAt(int index) => ref Unsafe.AddByteOffset(ref Unsafe.NullRef<TPrimitive>(),
        primitives + index * PrimitiveSize);

    public override void Render(PrimitiveType type, int primitiveCount, int vertices)
    {
        var vertexDataSize = primitiveCount * PrimitiveSize;
        if (bufferOffset + vertexDataSize > vertexBufferSize)
        {
            bufferOffset = 0;
            drawOffset = 0;
        }

        if (GpuCommandSync.WaitForRange(bufferOffset, vertexDataSize)) expandVertexBuffer();

        Unsafe.CopyBlock(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), bufferAddr + bufferOffset),
            ref Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), primitives), (uint)vertexDataSize);

        GL.FlushMappedNamedBufferRange(VertexBufferId, bufferOffset, vertexDataSize);

        var drawCount = primitiveCount * vertices;
        if (IndexBufferId != -1) GL.DrawElements(type, drawCount, DrawElementsType.UnsignedShort, drawOffset * sizeof(ushort));
        else GL.DrawArrays(type, drawOffset, drawCount);

        GpuCommandSync.LockRange(bufferOffset, vertexDataSize);

        bufferOffset += vertexDataSize;
        drawOffset += drawCount;
    }
    protected override void initializeVertexBuffer()
    {
        base.initializeVertexBuffer();
        vertexBufferSize = MinRenderableVertexCount * VertexDeclaration.VertexSize;

        GL.NamedBufferStorage(VertexBufferId, vertexBufferSize, 0,
            BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit);

        bufferAddr = GL.MapNamedBufferRange(VertexBufferId, 0, vertexBufferSize,
            BufferAccessMask.MapWriteBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapFlushExplicitBit |
            BufferAccessMask.MapUnsynchronizedBit | BufferAccessMask.MapInvalidateBufferBit);

        primitives = Marshal.AllocHGlobal(vertexBufferSize);
    }
    protected override void Dispose(bool disposing)
    {
        GL.UnmapNamedBuffer(VertexBufferId);
        Marshal.FreeHGlobal(primitives);

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

        // Rebuild the VBO

        GL.UnmapNamedBuffer(VertexBufferId);
        GL.DeleteBuffer(VertexBufferId);

        initializeVertexBuffer();

        // Rebuild the VAO

        Bind(CurrentShader);
        CurrentShader = null;

        bufferOffset = 0;
        drawOffset = 0;
    }

    public new static bool HasCapabilities() => GLFW.ExtensionSupported("GL_ARB_map_buffer_range") &&
        GpuCommandSync.HasCapabilities() && PrimitiveStreamerVao<TPrimitive>.HasCapabilities();
}