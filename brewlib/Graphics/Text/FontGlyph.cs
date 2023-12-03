using BrewLib.Graphics.Textures;
using System.Numerics;

namespace BrewLib.Graphics.Text;

public class FontGlyph(Texture2dRegion texture, int width, int height)
{
    public Texture2dRegion Texture => texture;
    public bool IsEmpty => texture is null;
    public int Width => width;
    public int Height => height;
    public Vector2 Size => new(width, height);

    public override string ToString() => $"{texture} {width}x{height}";
}