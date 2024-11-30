namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using OpenTK.Graphics.OpenGL;

public interface PrimitiveStreamer<TPrimitive> : IDisposable where TPrimitive : unmanaged
{
    ref TPrimitive PrimitiveAt(int index);

    void Bind(Shader shader);
    void Unbind();

    void Render(PrimitiveType type, int primitiveCount, int vertices);
}