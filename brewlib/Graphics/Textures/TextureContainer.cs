namespace BrewLib.Graphics.Textures;

using System;

public interface TextureContainer : IDisposable
{
    float UncompressedMemoryUseMb { get; }
    Texture2dRegion Get(string filename);
}