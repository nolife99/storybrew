namespace BrewLib.Graphics.Textures;

using System;

public interface Texture : IDisposable
{
    string Description { get; }
    Texture2d BindableTexture { get; }
}