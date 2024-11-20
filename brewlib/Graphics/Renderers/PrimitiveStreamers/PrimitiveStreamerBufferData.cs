namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using OpenTK.Graphics.OpenGL;

public class PrimitiveStreamerBufferData<TPrimitive>(VertexDeclaration vertexDeclaration,
    int minRenderableVertexCount,
    ReadOnlySpan<ushort> indexes)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indexes) where TPrimitive : unmanaged
{
    public override void Render(PrimitiveType type, nint primitives, int count, int drawCount, bool canBuffer)
    {
        GL.BufferData(BufferTarget.ArrayBuffer, count * PrimitiveSize, primitives, BufferUsageHint.StaticDraw);
        ++DiscardedBufferCount;

        if (IndexBufferId != -1) GL.DrawElements(type, drawCount, DrawElementsType.UnsignedShort, 0);
        else GL.DrawArrays(type, 0, drawCount);
    }
    protected override void internalBind(Shader shader)
    {
        base.internalBind(shader);
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
    }
    public new static bool HasCapabilities()
        => DrawState.HasCapabilities(1, 5) && PrimitiveStreamerVao<TPrimitive>.HasCapabilities();
}