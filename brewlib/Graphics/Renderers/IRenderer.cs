namespace BrewLib.Graphics.Renderers;

using System;
using Cameras;

public interface IRenderer : IDisposable
{
    ICamera Camera { get; set; }

    void BeginRendering();
    void EndRendering();

    void Flush(bool canBuffer = false);
}

public interface IPrimitiveRenderer : IRenderer
{
    PrimitiveTopology Topology { get; }
    PrimitiveBatchFeatures BatchFeatures { get; }
}
