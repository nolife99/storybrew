namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;

public class PrimitiveStreamerBufferData<TPrimitive>(VertexDeclaration vertexDeclaration,
    int minRenderableVertexCount,
    ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indices) where TPrimitive : allows ref struct
{
    nint primitives;

    public override ref TPrimitive PrimitiveAt(int index)
        => ref Unsafe.AddByteOffset(ref Unsafe.NullRef<TPrimitive>(), primitives + index * PrimitiveSize);

    public override void Render(PrimitiveType type, int primitiveCount, int vertices)
    {
        var vertexDataSize = primitiveCount * PrimitiveSize;
        GL.NamedBufferSubData(VertexBufferId, 0, vertexDataSize, primitives);

        if (IndexBufferId != -1) GL.DrawElements(type, primitiveCount * vertices, DrawElementsType.UnsignedShort, 0);
        else GL.DrawArrays(type, 0, primitiveCount * vertices);
    }
    protected override void initializeVertexBuffer()
    {
        base.initializeVertexBuffer();

        var vertexBufferSize = MinRenderableVertexCount * VertexDeclaration.VertexSize;
        primitives = Marshal.AllocHGlobal(vertexBufferSize);

        GL.NamedBufferStorage(VertexBufferId, vertexBufferSize, 0,
            BufferStorageFlags.MapWriteBit | BufferStorageFlags.DynamicStorageBit);
    }
    protected override void Dispose(bool disposing)
    {
        Marshal.FreeHGlobal(primitives);
        base.Dispose(disposing);
    }
}