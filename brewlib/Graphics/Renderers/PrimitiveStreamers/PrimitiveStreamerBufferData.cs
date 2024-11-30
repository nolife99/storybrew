namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;

public class PrimitiveStreamerBufferData<TPrimitive>(VertexDeclaration vertexDeclaration,
    int minRenderableVertexCount,
    ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indices) where TPrimitive : unmanaged
{
    readonly nint primitives = Marshal.AllocHGlobal(minRenderableVertexCount * Marshal.SizeOf<TPrimitive>());

    public override ref TPrimitive PrimitiveAt(int index)
        => ref Unsafe.AddByteOffset(ref Unsafe.NullRef<TPrimitive>(), primitives + index * PrimitiveSize);

    public override void Render(PrimitiveType type, int primitiveCount, int vertices)
    {
        GL.NamedBufferData(VertexBufferId, primitiveCount * PrimitiveSize, primitives, BufferUsageHint.StreamDraw);

        var drawCount = primitiveCount * vertices;
        if (IndexBufferId != -1) GL.DrawElements(type, drawCount, DrawElementsType.UnsignedShort, 0);
        else GL.DrawArrays(type, 0, drawCount);
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        Marshal.FreeHGlobal(primitives);
    }
}