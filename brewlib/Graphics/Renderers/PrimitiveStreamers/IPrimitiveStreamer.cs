namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using OpenTK.Graphics.OpenGL;
using Shaders;

internal interface IPrimitiveStreamer<TPrimitive> : IDisposable where TPrimitive : struct
{
    int QueuedRenders { get; }
    int PrimitivesInBatch { get; }
    void AddPrimitive(ref readonly TPrimitive primitive);

    void Bind(Shader shader);
    void Unbind();

    void Render(PrimitiveType type, int vertexCount);

    void QueueRender(int indexCount, int vertexCount);
}