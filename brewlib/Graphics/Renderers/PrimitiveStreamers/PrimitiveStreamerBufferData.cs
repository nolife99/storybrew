using osuTK.Graphics.OpenGL;
using System.Diagnostics;

namespace BrewLib.Graphics.Renderers.PrimitiveStreamers
{
    public class PrimitiveStreamerBufferData<TPrimitive>(VertexDeclaration vertexDeclaration, int minRenderableVertexCount, ushort[] indexes = null) : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indexes) where TPrimitive : struct
    {
        public override void Render(PrimitiveType primitiveType, TPrimitive[] primitives, int primitiveCount, int drawCount, bool canBuffer = false)
        {
            Debug.Assert(primitiveCount <= primitives.Length);
            Debug.Assert((drawCount & primitiveCount) == 0);

            GL.BufferData(BufferTarget.ArrayBuffer, primitiveCount * PrimitiveSize, primitives, BufferUsageHint.StaticDraw);
            ++DiscardedBufferCount;

            if (IndexBufferId != -1) GL.DrawElements(primitiveType, drawCount, DrawElementsType.UnsignedShort, 0);
            else GL.DrawArrays(primitiveType, 0, drawCount);
        }
        protected override void internalBind(Shader shader)
        {
            base.internalBind(shader);
            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
        }
        public new static bool HasCapabilities() => DrawState.HasCapabilities(1, 5) && PrimitiveStreamerVao<TPrimitive>.HasCapabilities();
    }
}