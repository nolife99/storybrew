namespace BrewLib.Graphics.Textures;

using SixLabors.ImageSharp;

public interface ITrimmedTextureRegion : ITextureRegion
{
    Rectangle ContentBounds { get; }
}
