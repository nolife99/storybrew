namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;

public class PrimitiveStreamerBufferData<TPrimitive>(VertexDeclaration vertexDeclaration,
    int minRenderableVertexCount,
    ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indices) where TPrimitive : unmanaged
{
    public override void Render(PrimitiveType type, ReadOnlySpan<TPrimitive> primitives, int vertices)
    {
        GL.BufferData(BufferTarget.ArrayBuffer, primitives.Length * PrimitiveSize, ref MemoryMarshal.GetReference(primitives),
            BufferUsageHint.StaticDraw);

        ++DiscardedBufferCount;

        var drawCount = primitives.Length * vertices;
        if (IndexBufferId != -1) GL.DrawElements(type, drawCount, DrawElementsType.UnsignedShort, 0);
        else GL.DrawArrays(type, 0, drawCount);
    }
    protected override void internalBind(Shader shader)
    {
        base.internalBind(shader);
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
    }
}