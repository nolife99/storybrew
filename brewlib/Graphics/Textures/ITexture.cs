namespace BrewLib.Graphics.Textures;

using System;
using System.Numerics;
using Backend;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public interface ITextureExtent
{
    Size Size { get; }
    int Width { get; }
    int Height { get; }
}

public interface ITexture : IDisposable, ITextureExtent
{
    IGraphicsBackend Backend { get; }
    GraphicsResourceHandle NativeHandle { get; }
}

interface ITextureSamplerIdentity
{
    GraphicsResourceHandle SamplerIdentity { get; }
}

public interface IWritableTexture : ITexture, ITextureRegion
{
    void Update(Color color, int x, int y, int width, int height);
    void Update(Image<Rgba32> bitmap, int x, int y);
}

public interface ITextureRegion : IDisposable, ITextureExtent
{
    ITexture Texture { get; }
    Rectangle Bounds { get; }
    Vector2 UvOrigin { get; }
    Vector2 UvRatio { get; }
}

public interface ITextureFactory
{
    ITextureRegion Load(Image<Rgba32> bitmap, TextureOptions textureOptions = null);
    IWritableTexture Create(Color color, int width = 1, int height = 1, TextureOptions textureOptions = null);
}