using System;
using osuTK.Graphics.OpenGL;

namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

public interface PrimitiveStreamer<TPrimitive> : IDisposable where TPrimitive : unmanaged
{
    int DiscardedBufferCount { get; }
    int BufferWaitCount { get; }

    void Bind(Shader shader);
    void Unbind();

    unsafe void Render(PrimitiveType primitiveType, TPrimitive* primitives, int primitiveCount, int drawCount, bool canBuffer = false);
}