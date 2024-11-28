namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

public interface LineRenderer : Renderer, IDisposable
{
    void Draw(ref readonly Vector3 start, ref readonly Vector3 end, ref readonly Rgba32 color);
}