namespace BrewLib.Graphics.Text;

using System.Numerics;
using Textures;

public class FontGlyph(Texture2dRegion texture, int width, int height)
{
    public Texture2dRegion Texture => texture;
    public bool IsEmpty => texture is null;
    public int Width => width;
    public int Height => height;
    public Vector2 Size => new(width, height);
}