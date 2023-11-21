using BrewLib.Graphics.Textures;
using osuTK;

namespace BrewLib.Graphics.Text
{
    public class FontGlyph(Texture2dRegion texture, int width, int height)
    {
        readonly Texture2dRegion texture = texture;
        public Texture2dRegion Texture => texture;
        public bool IsEmpty => texture == null;

        readonly int width = width, height = height;
        public int Width => width;
        public int Height => height;
        public Vector2 Size => new(width, height);

        public override string ToString() => $"{texture} {width}x{height}";
    }
}