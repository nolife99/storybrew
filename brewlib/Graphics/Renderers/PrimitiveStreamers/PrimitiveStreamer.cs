namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using OpenTK.Graphics.OpenGL;

public interface PrimitiveStreamer : IDisposable
{
    int DiscardedBufferCount { get; }
    int BufferWaitCount { get; }

    void Bind(Shader shader);
    void Unbind();

    unsafe void Render(PrimitiveType type, void* primitives, int count, int drawCount, bool canBuffer = false);
}