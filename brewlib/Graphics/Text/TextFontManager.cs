namespace BrewLib.Graphics.Text;

using System;
using BrewLib.Graphics.Textures;
using Tiny.PooledCollections.Generic;

public sealed class TextFontManager(TextureContainer container) : IDisposable
{
    readonly PooledDictionary<int, TextFontAtlased> fonts = new();
    readonly PooledDictionary<int, int> references = new();

    public TextFont GetTextFont(string fontName, float fontSize, float scaling)
    {
        var identifier = HashCode.Combine(fontName, fontSize, scaling);
        if (!fonts.TryGetValue(identifier, out var font))
            fonts[identifier] = font = new(fontName, fontSize * scaling, container);

        if (references.TryGetValue(identifier, out var refCount)) references[identifier] = refCount + 1;
        else references[identifier] = 1;

        return new TextFontProxy<(int, TextFontManager)>(font,
            (f, state) =>
            {
                if (--state.Item2.references[state.Item1] != 0) return;

                state.Item2.fonts.Remove(state.Item1);
                f.Dispose();
            },
            (identifier, this));
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        foreach (var font in fonts.Values) font.Dispose();
        fonts.Dispose();
        references.Dispose();
        disposed = true;
    }

    #endregion
}