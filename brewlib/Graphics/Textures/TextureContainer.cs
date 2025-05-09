namespace BrewLib.Graphics.Textures;

using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public interface TextureContainer : IDisposable
{
    float UncompressedMemoryUseMb { get; }
    Texture2dRegion Get(string filename);
    Texture2dRegion Add(Image<Rgba32> bitmap, TextureOptions options = null);
}