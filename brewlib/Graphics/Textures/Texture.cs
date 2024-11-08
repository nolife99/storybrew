namespace BrewLib.Graphics.Textures;

using System;

public interface Texture : IDisposable
{
    string Description { get; }
    BindableTexture BindableTexture { get; }
}