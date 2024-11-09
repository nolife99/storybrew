﻿namespace BrewLib.Graphics.Textures;

using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using Util;

public sealed class TextureContainerAtlas(ResourceContainer resourceContainer = null,
    TextureOptions textureOptions = null,
    int width = 512,
    int height = 512,
    int padding = 0,
    string description = nameof(TextureContainerAtlas)) : TextureContainer
{
    Dictionary<TextureOptions, TextureMultiAtlas2d> atlases = [];
    Dictionary<string, Texture2dRegion> textures = [];

    public float UncompressedMemoryUseMb => textures.Values.Sum(texture => texture.Size.X * texture.Size.Y) / 1048576;

    public event ResourceLoadedDelegate<Texture2dRegion> ResourceLoaded;

    public Texture2dRegion Get(string filename)
    {
        filename = PathHelper.WithStandardSeparators(filename);
        if (!textures.TryGetValue(filename, out var texture))
        {
            var options = textureOptions ?? Texture2d.LoadTextureOptions(filename, resourceContainer) ?? TextureOptions.Default;
            if (!atlases.TryGetValue(options, out var atlas))
                atlases.Add(options, atlas = new(width, height, $"{description} (Option set {atlases.Count})", options, padding));

            using (var bitmap = Texture2d.LoadBitmap(filename, resourceContainer))
                if (bitmap is not null)
                    texture = atlas.AddRegion(bitmap, filename);

            textures.Add(filename, texture);
            ResourceLoaded?.Invoke(filename, texture);
        }

        return texture;
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (!disposed)
        {
            atlases.Dispose();
            textures.Clear();

            atlases = null;
            textures = null;

            GC.SuppressFinalize(this);
            disposed = true;
        }
    }

    #endregion
}