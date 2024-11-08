namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using osuTK.Graphics.OpenGL;

public interface PrimitiveStreamer : IDisposable
{
    int DiscardedBufferCount { get; }
    int BufferWaitCount { get; }

    void Bind(Shader shader);
    void Unbind();

    unsafe void Render(PrimitiveType primitiveType, void* primitives, int primitiveCount, int drawCount,
        bool canBuffer = false);
}