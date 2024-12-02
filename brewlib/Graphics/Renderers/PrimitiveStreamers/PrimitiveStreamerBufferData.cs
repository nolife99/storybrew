namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using OpenTK.Graphics.OpenGL;
using Util;

public class PrimitiveStreamerBufferData<TPrimitive>(VertexDeclaration vertexDeclaration,
    int minRenderableVertexCount,
    ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indices) where TPrimitive : struct
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

        GL.InvalidateBufferSubData(VertexBufferId, 0, vertexDataSize);
    }
    protected override void initializeVertexBuffer()
    {
        base.initializeVertexBuffer();
        primitives = Native.AllocateMemory(MinRenderableVertexCount * VertexDeclaration.VertexSize);

        GL.NamedBufferStorage(VertexBufferId, MinRenderableVertexCount * VertexDeclaration.VertexSize, 0, BufferStorageFlags.MapWriteBit | BufferStorageFlags.DynamicStorageBit);
    }
    protected override void Dispose(bool disposing)
    {
        Native.FreeMemory(primitives);
        base.Dispose(disposing);
    }
}