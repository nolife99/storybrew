namespace BrewLib.Graphics.Textures;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Util;

public sealed class TextureContainerAtlas(ResourceContainer resourceContainer = null,
    TextureOptions textureOptions = null,
    int width = 1024,
    int height = 1024,
    int padding = 0,
    string atlasDescription = nameof(TextureContainerAtlas)) : TextureContainer
{
    readonly Dictionary<TextureOptions, TextureMultiAtlas2d> atlases = [];
    readonly Dictionary<string, Texture2dRegion> textures = [];

    public float UncompressedMemoryUseMb
    {
        get
        {
            var sum = 0f;
            foreach (var texture in textures.Values)
                if (texture is not null)
                {
                    var size = texture.Size;
                    sum += size.X * size.Y;
                }

            return sum / 1024 / 1024;
        }
    }

    public Texture2dRegion Get(string filename)
    {
        PathHelper.WithStandardSeparatorsUnsafe(filename);
        ref var texture = ref CollectionsMarshal.GetValueRefOrAddDefault(textures, filename, out var exists);
        if (exists) return texture;

        var options = textureOptions ?? Texture2d.LoadTextureOptions(filename, resourceContainer);
        return texture = Add(Texture2d.LoadBitmap(filename, resourceContainer), filename, options);
    }

    public Texture2dRegion Add(Image<Rgba32> bitmap, string description, TextureOptions options)
    {
        if (bitmap is null) return null;

        options ??= TextureOptions.Default;
        ref var atlas = ref CollectionsMarshal.GetValueRefOrAddDefault(atlases, options, out var exists);
        if (!exists) atlas = new(width, height, $"{atlasDescription} (Option set {atlases.Count})", options, padding);

        return atlas.AddRegion(bitmap, description);
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        atlases.Dispose();
        disposed = true;
    }

    #endregion
}