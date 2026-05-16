namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using BrewLib.Graphics.Shaders;

interface IPrimitiveStreamer<TPrimitive> : IDisposable where TPrimitive : unmanaged
{
    ref TPrimitive PrimitiveAt(int index);

    void Bind(Shader shader);
    void Unbind();

    void Render(PrimitiveTopology topology, int primitiveCount, int verticesPerPrimitive);
}
