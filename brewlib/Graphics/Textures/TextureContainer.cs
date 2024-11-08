namespace BrewLib.Graphics.Textures;

using System;

public delegate void ResourceLoadedDelegate<T>(string filename, T resource);

public interface TextureContainer : IDisposable
{
    float UncompressedMemoryUseMb { get; }
    event ResourceLoadedDelegate<Texture2dRegion> ResourceLoaded;

    Texture2dRegion Get(string filename);
}