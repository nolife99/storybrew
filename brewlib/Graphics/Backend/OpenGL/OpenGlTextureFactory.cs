namespace BrewLib.Graphics.Backend.OpenGL;

using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public sealed class OpenGlTextureFactory(IGraphicsBackend backend) : ITextureFactory
{
    public ITextureRegion Load(Image<Rgba32> bitmap, TextureOptions textureOptions = null)
        => Texture2d.Load(bitmap, textureOptions, backend);

    public IWritableTexture Create(Color color,
        int width = 1,
        int height = 1,
        TextureOptions textureOptions = null)
        => Texture2d.Create(color, width, height, textureOptions, backend);
}
