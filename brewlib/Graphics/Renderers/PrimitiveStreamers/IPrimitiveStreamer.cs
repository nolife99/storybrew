namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using OpenTK.Graphics.OpenGL;
using Shaders;

internal interface IPrimitiveStreamer<TPrimitive> : IDisposable where TPrimitive : struct, allows ref struct
{
    int QueuedRenders { get; }
    int PrimitivesInBatch { get; }
    void AddPrimitive(ref readonly TPrimitive primitive, int vertexCount);

    void Bind(Shader shader);
    void Unbind();

    void Render(PrimitiveType type, int vertexCount);

    void QueueRender(int indexCount, int vertexCount);
}