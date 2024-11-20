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

    void Draw(ref readonly Vector3 start, ref readonly Vector3 end, ref readonly Rgba32 color);
}