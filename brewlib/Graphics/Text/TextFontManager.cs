namespace BrewLib.Graphics.Text;

using System;
using Collections.Pooled;
using Textures;

public sealed class TextFontManager(TextureContainer container) : IDisposable
{
    readonly PooledDictionary<int, TextFontAtlased> fonts = new();
    readonly PooledDictionary<int, int> references = new();

    public TextFont GetTextFont(string fontName, float fontSize, float scaling)
    {
        var identifier = HashCode.Combine(fontName, fontSize, scaling);
        if (!fonts.TryGetValue(identifier, out var font))
            fonts[identifier] = font = new(container, fontName, fontSize * scaling);

        if (references.TryGetValue(identifier, out var refCount)) references[identifier] = refCount + 1;
        else references[identifier] = 1;

        return new TextFontProxy(font,
            () =>
            {
                if (--references[identifier] != 0) return;

                fonts.Remove(identifier);
                font.Dispose();
            });
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