namespace BrewLib.Graphics.Text;

using System.Numerics;
using Textures;

public record FontGlyph(Texture2dRegion Texture, int Width, int Height)
{
    public bool IsEmpty => Texture is null;
    public Vector2 Size => new(Width, Height);
}