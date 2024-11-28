namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using OpenTK.Graphics.OpenGL;

public interface PrimitiveStreamer<TPrimitive> : IDisposable where TPrimitive : unmanaged
{
    int DiscardedBufferCount { get; }
    int BufferWaitCount { get; }

    void Bind(Shader shader);
    void Unbind();

    void Render(PrimitiveType type, ReadOnlySpan<TPrimitive> primitives, int vertices);
}