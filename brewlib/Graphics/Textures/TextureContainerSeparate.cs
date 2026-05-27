namespace BrewLib.Graphics.Textures;

using System;
using System.Collections.Generic;
using System.Threading;
using IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Util;

public sealed class TextureContainerSeparate(ITextureFactory textureFactory,
    ResourceContainer resourceContainer = null,
    TextureOptions textureOptions = null) : TextureContainer
{
    readonly ITextureFactory textureFactory = textureFactory ?? DrawState.Backend?.TextureFactory ??
        throw new InvalidOperationException("Texture containers require DrawState to be initialized with a graphics backend");

    readonly Lock writeLock = new();

    volatile Dictionary<string, ITextureRegion> textures = new(StringComparer.OrdinalIgnoreCase);

    public long UncompressedMemoryUse
    {
        get
        {
            var pixels = 0L;
            foreach (var texture in textures.Values)
                if (texture is not null)
                {
                    var size = texture.Size;
                    pixels += size.Width * size.Height;
                }

            return pixels * 4;
        }
    }

    public ITextureRegion Get(scoped ReadOnlySpan<char> filename)
    {
        var snapshot = textures;
        if (snapshot.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(filename, out var texture))
            return texture;

        var str = filename.ToString();
        using var bitmap = TextureLoader.LoadBitmap(str, resourceContainer);
        texture = bitmap is not null
            ? textureFactory.Load(bitmap, textureOptions ?? TextureLoader.LoadTextureOptions(str, resourceContainer))
            : null;

        return GetOrAdd(str, texture);
    }

    public bool TryGetLoaded(scoped ReadOnlySpan<char> filename, out ITextureRegion texture)
        => textures.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(filename, out texture);

    public ITextureRegion Add(Image<Rgba32> bitmap, TextureOptions options = null)
        => bitmap is not null ? textureFactory.Load(bitmap, options ?? textureOptions) : null;

    public ITextureRegion Add(string filename, Image<Rgba32> bitmap, TextureOptions options = null)
    {
        if (bitmap is null) return null;

        filename = PathHelper.WithStandardSeparators(filename);

        if (textures.TryGetValue(filename, out var existing)) return existing;

        var texture = textureFactory.Load(bitmap, options ?? textureOptions);
        return GetOrAdd(filename, texture);
    }

    public ITextureRegion Add(string filename, PreparedTextureUpload upload, IAsyncTextureUploader uploader)
    {
        if (upload is null || uploader is null) return null;

        filename = PathHelper.WithStandardSeparators(filename);

        if (textures.TryGetValue(filename, out var existing)) return existing;

        var texture = uploader.Upload(upload);
        return GetOrAdd(filename, texture);
    }

    ITextureRegion GetOrAdd(string key, ITextureRegion candidate)
    {
        lock (writeLock)
        {
            if (textures.TryGetValue(key, out var winner))
            {
                candidate?.Dispose();
                return winner;
            }

            var next = new Dictionary<string, ITextureRegion>(textures, StringComparer.Ordinal)
            {
                [key] = candidate
            };

            textures = next;
            return candidate;
        }
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        foreach (var texture in textures.Values) texture?.Dispose();
        disposed = true;
    }

    #endregion
}