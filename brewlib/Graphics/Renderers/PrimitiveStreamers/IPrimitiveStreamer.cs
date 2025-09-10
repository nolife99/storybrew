namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using BrewLib.Graphics.Shaders;
using OpenTK.Graphics.OpenGL;

interface IPrimitiveStreamer<TPrimitive> : IDisposable where TPrimitive : unmanaged
{
    int QueuedRenders { get; }
    int PrimitivesInBatch { get; }
    GpuCommandSync FrameSync { get; }

    void AddPrimitive(scoped ref readonly TPrimitive primitive);

    void Bind(Shader shader);
    void Unbind();

    void Render(PrimitiveType type, int vertexCount);

    void QueueRender(int indexCount, int vertexCount);
}