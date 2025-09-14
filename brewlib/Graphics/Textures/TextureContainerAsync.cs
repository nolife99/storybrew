namespace BrewLib.Graphics.Textures;

using System;
using System.Linq;
using System.Threading.Tasks;
using BrewLib.IO;
using BrewLib.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tiny.PooledCollections.Generic;

public sealed class TextureContainerAsync : TextureContainer
{
    readonly ResourceContainer resourceContainer;
    readonly TextureOptions textureOptions;

    readonly PooledDictionary<string, Task<Texture2d>> textures;
    readonly PooledDictionary<string, Task<Texture2d>>.AlternateLookup<ReadOnlySpan<char>> texturesLookup;

    public TextureContainerAsync(ResourceContainer resourceContainer = null, TextureOptions textureOptions = null)
    {
        this.resourceContainer = resourceContainer;
        this.textureOptions = textureOptions;

        textures = new();
        texturesLookup = textures.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public long UncompressedMemoryUse
    {
        get
        {
            var pixels = 0L;
            foreach (var texture in textures.Values)
                if (texture.IsCompleted && texture.Result is not null)
                {
                    var size = texture.Result.Size;
                    pixels += (long)(size.X * size.Y);
                }

            return pixels * 4;
        }
    }

    public Texture2dRegion Get(scoped ReadOnlySpan<char> filename)
    {
        switch (texturesLookup.TryGetValue(filename, out var texture))
        {
            case true when texture.IsCompleted: return texture.Result;

            case false:
                var str = filename.ToString();
                var bitmap = Texture2d.LoadBitmapAsync(str, resourceContainer);

                if (bitmap.IsCompleted && bitmap.Result is null) return null;

                textures[str] = bitmap.ContinueWith((b, opt) =>
                    {
                        using var img = b.Result;
                        return Texture2d.LoadAsync(img, (TextureOptions)opt).Result;
                    },
                    textureOptions);

                break;
        }

        return DrawState.TransparentPixel;
    }

    public Texture2dRegion Add(Image<Rgba32> bitmap, TextureOptions options = null)
        => Texture2d.Load(bitmap, textureOptions);

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        Task.WhenAll(textures.Select(x
                => x.Value.ContinueWith(async t
                    => await Native.MainThreadScheduler(a => ((Texture2d)a).Dispose(), await t))))
            .Wait();

        textures.Dispose();
        disposed = true;
    }

    #endregion
}