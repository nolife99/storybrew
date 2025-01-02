namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using OpenTK.Graphics.OpenGL;
using Shaders;
using Util;

public class PrimitiveStreamerBufferData<TPrimitive>(VertexDeclaration vertexDeclaration,
    int minRenderableVertexCount,
    ReadOnlySpan<ushort> indices) : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indices)
    where TPrimitive : struct, allows ref struct
{
    nint primitives;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ref TPrimitive PrimitiveAt(int index) => ref Unsafe.AddByteOffset(
        ref Unsafe.NullRef<TPrimitive>(),
        primitives + index * PrimitiveSize);

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
        primitives = Native.AllocateMemory(vertexBufferSize);

        GL.NamedBufferStorage(VertexBufferId,
            vertexBufferSize,
            0,
            BufferStorageFlags.MapWriteBit | BufferStorageFlags.DynamicStorageBit);
    }

    protected override void Dispose(bool disposing)
    {
        Native.FreeMemory(primitives);
        base.Dispose(disposing);
    }
}