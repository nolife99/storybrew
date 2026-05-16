namespace BrewLib.Graphics.Textures;

using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public interface TextureContainer : IDisposable
{
    long UncompressedMemoryUse { get; }
    ITextureRegion Get(scoped ReadOnlySpan<char> filename);
    ITextureRegion Add(Image<Rgba32> bitmap, TextureOptions options = null);
}
