namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using osuTK.Graphics.OpenGL;

public class PrimitiveStreamerBufferData<TPrimitive>(
    VertexDeclaration vertexDeclaration, int minRenderableVertexCount, ushort[] indexes = null)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indexes)
    where TPrimitive : unmanaged
{
    public override unsafe void Render(PrimitiveType primitiveType, void* primitives, int primitiveCount, int drawCount,
        bool canBuffer = false)
    {
        GL.BufferData(BufferTarget.ArrayBuffer, primitiveCount * PrimitiveSize, (nint)primitives,
            BufferUsageHint.StaticDraw);
        ++DiscardedBufferCount;

        if (IndexBufferId != -1)
            GL.DrawElements(primitiveType, drawCount, DrawElementsType.UnsignedShort, 0);
        else
            GL.DrawArrays(primitiveType, 0, drawCount);
    }

    protected override void internalBind(Shader shader)
    {
        base.internalBind(shader);
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
    }

    public new static bool HasCapabilities()
        => DrawState.HasCapabilities(1, 5) && PrimitiveStreamerVao<TPrimitive>.HasCapabilities();
}