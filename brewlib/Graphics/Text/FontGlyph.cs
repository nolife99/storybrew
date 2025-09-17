namespace BrewLib.Graphics.Text;

using System.Numerics;
using BrewLib.Graphics.Textures;

public readonly record struct FontGlyph(Texture2dRegion Texture, int width, int height)
{
    public readonly Vector2 Size = new(width, height);
    public bool IsEmpty => Texture is null;
}