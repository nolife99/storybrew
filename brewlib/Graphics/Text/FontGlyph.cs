namespace BrewLib.Graphics.Text;

using System.Numerics;
using BrewLib.Graphics.Textures;

public readonly record struct FontGlyph(Texture2dRegion Texture, int Width, int Height)
{
    public bool IsEmpty => Texture is null;
    public Vector2 Size => new(Width, Height);
}