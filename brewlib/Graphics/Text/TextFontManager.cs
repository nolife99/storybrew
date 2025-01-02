namespace BrewLib.Graphics.Text;

using System;
using System.Collections.Generic;
using Util;

public sealed class TextFontManager : IDisposable
{
    readonly Dictionary<int, TextFontAtlased> fonts = [];
    readonly Dictionary<int, int> references = [];

    public TextFont GetTextFont(string fontName, float fontSize, float scaling)
    {
        var identifier = HashCode.Combine(fontName, fontSize, scaling);
        if (!fonts.TryGetValue(identifier, out var font)) fonts[identifier] = font = new(fontName, fontSize * scaling);
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

        fonts.Dispose();
        disposed = true;
    }

    #endregion
}