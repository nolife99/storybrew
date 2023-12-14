using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Textures;

namespace BrewLib.Graphics.Renderers;

public interface SpriteRenderer : Renderer, IDisposable
{
    Shader Shader { get; }
    Matrix4x4 TransformMatrix { get; set; }

    int RenderedSpriteCount { get; }
    int FlushedBufferCount { get; }
    int DiscardedBufferCount { get; }
    int BufferWaitCount { get; }
    int LargestBatch { get; }

    void Draw(Texture2dRegion texture, float x, float y, float originX, float originY, float scaleX, float scaleY, float rotation, Color color);
    void Draw(Texture2dRegion texture, float x, float y, float originX, float originY, float scaleX, float scaleY, float rotation, Color color, float textureX0, float textureY0, float textureX1, float textureY1);
}
[StructLayout(LayoutKind.Sequential)] public struct SpritePrimitive
{
    public float x1, y1, u1, v1; public int color1;
    public float x2, y2, u2, v2; public int color2;
    public float x3, y3, u3, v3; public int color3;
    public float x4, y4, u4, v4; public int color4;
}