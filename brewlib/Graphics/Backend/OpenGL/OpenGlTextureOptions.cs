namespace BrewLib.Graphics.Backend.OpenGL;

using Silk.NET.OpenGL;
using Textures;

static class OpenGlTextureOptions
{
    public static void ApplyParameters(TextureOptions options, TextureTarget texture)
    {
        OpenGlApi.GL.TexParameter(texture,
            TextureParameterName.TextureMinFilter,
            (int)(options.TextureMinFilter switch
            {
                TextureFilter.Nearest => TextureMinFilter.Nearest,
                TextureFilter.NearestMipmapNearest => TextureMinFilter.NearestMipmapNearest,
                TextureFilter.LinearMipmapNearest => TextureMinFilter.LinearMipmapNearest,
                TextureFilter.NearestMipmapLinear => TextureMinFilter.NearestMipmapLinear,
                TextureFilter.LinearMipmapLinear => TextureMinFilter.LinearMipmapLinear,
                _ => TextureMinFilter.Linear
            }));

        OpenGlApi.GL.TexParameter(texture,
            TextureParameterName.TextureMagFilter,
            (int)(options.TextureMagFilter switch
            {
                TextureFilter.Nearest => TextureMagFilter.Nearest,
                _ => TextureMagFilter.Linear
            }));

        OpenGlApi.GL.TexParameter(texture,
            TextureParameterName.TextureWrapS,
            (int)toOpenGlWrap(options.TextureWrapS));

        OpenGlApi.GL.TexParameter(texture,
            TextureParameterName.TextureWrapT,
            (int)toOpenGlWrap(options.TextureWrapT));
    }

    static TextureWrapMode toOpenGlWrap(TextureWrap wrap)
        => wrap switch
        {
            TextureWrap.Clamp => TextureWrapMode.ClampToEdge,
            TextureWrap.Repeat => TextureWrapMode.Repeat,
            TextureWrap.MirroredRepeat => TextureWrapMode.MirroredRepeat,
            TextureWrap.ClampToBorder => TextureWrapMode.ClampToBorder,
            _ => TextureWrapMode.ClampToEdge
        };
}