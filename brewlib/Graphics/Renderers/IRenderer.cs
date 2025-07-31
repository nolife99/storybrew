namespace BrewLib.Graphics.Renderers;

using System;
using BrewLib.Graphics.Cameras;

public interface IRenderer : IDisposable
{
    ICamera Camera { get; set; }

    void BeginRendering();
    void EndRendering();

    void Flush(bool canBuffer = false);
}