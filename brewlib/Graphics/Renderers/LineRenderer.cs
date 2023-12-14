using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;

namespace BrewLib.Graphics.Renderers;

public interface LineRenderer : Renderer, IDisposable
{
    Shader Shader { get; }
    Matrix4x4 TransformMatrix { get; set; }

    int RenderedLineCount { get; }
    int FlushedBufferCount { get; }
    int DiscardedBufferCount { get; }
    int BufferWaitCount { get; }
    int LargestBatch { get; }

    void Draw(Vector3 start, Vector3 end, Color color);
    void Draw(Vector3 start, Vector3 end, Color startColor, Color endColor);
}
[StructLayout(LayoutKind.Sequential)]
public struct LinePrimitive
{
    public float x1, y1, z1; public int color1;
    public float x2, y2, z2; public int color2;
}