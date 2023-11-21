using osuTK.Graphics.OpenGL;
using System;

namespace BrewLib.Graphics.Renderers.PrimitiveStreamers
{
    public interface PrimitiveStreamer<TPrimitive> : IDisposable where TPrimitive : struct
    {
        int DiscardedBufferCount { get; }
        int BufferWaitCount { get; }

        void Bind(Shader shader);
        void Unbind();

        void Render(PrimitiveType primitiveType, TPrimitive[] primitives, int primitiveCount, int drawCount, bool canBuffer = false);
    }
}