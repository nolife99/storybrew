namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using OpenTK.Graphics.OpenGL;
using Shaders;

public interface IPrimitiveStreamer<TPrimitive> : IDisposable where TPrimitive : struct, allows ref struct
{
    void AddPrimitive(ref readonly TPrimitive primitive);

    void Bind(Shader shader);
    void Unbind();

    void Render(PrimitiveType type);

    int QueuedRenders { get; }
    int PrimitivesInBatch { get; }

    void QueueRender(int vertexCount);
}