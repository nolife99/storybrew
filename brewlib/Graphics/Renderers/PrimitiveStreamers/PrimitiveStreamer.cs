namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using OpenTK.Graphics.OpenGL;

public interface PrimitiveStreamer : IDisposable
{
    int DiscardedBufferCount { get; }
    int BufferWaitCount { get; }

    void Bind(Shader shader);
    void Unbind();

    void Render(PrimitiveType type, nint primitives, int count, int drawCount);
}