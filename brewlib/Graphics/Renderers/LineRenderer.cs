namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

public interface LineRenderer : Renderer, IDisposable
{
    Shader Shader { get; }
    Matrix4x4 TransformMatrix { get; set; }

    int RenderedLineCount { get; }
    int FlushedBufferCount { get; }
    int DiscardedBufferCount { get; }
    int BufferWaitCount { get; }
    int LargestBatch { get; }

    void Draw(Vector3 start, Vector3 end, Rgba32 color);
    void Draw(Vector3 start, Vector3 end, Rgba32 startColor, Rgba32 endColor);
}