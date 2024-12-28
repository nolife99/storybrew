namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using OpenTK.Graphics.OpenGL;
using Shaders;

public interface IPrimitiveStreamer<TPrimitive> : IDisposable where TPrimitive : struct, allows ref struct
{
    ref TPrimitive PrimitiveAt(int index);

    void Bind(Shader shader);
    void Unbind();

    void Render(PrimitiveType type, int primitiveCount, int vertices);
}