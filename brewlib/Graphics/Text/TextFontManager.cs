using BrewLib.Util;
using System;
using System.Collections.Generic;

namespace BrewLib.Graphics.Text;

public class TextFontManager : IDisposable
{
    Dictionary<string, TextFontAtlased> fonts = [];
    readonly Dictionary<string, int> references = [];

    public TextFont GetTextFont(string fontName, float fontSize, float scaling)
    {
        var identifier = $"{fontName}|{fontSize}|{scaling}";

        if (!fonts.TryGetValue(identifier, out var font)) fonts.Add(identifier, font = new(fontName, fontSize * scaling));
        if (references.TryGetValue(identifier, out int refCount)) references[identifier] = refCount + 1;
        else references[identifier] = 1;

        return new TextFontProxy(font, () =>
        {
            var remaining = --references[identifier];
            if (remaining == 0)
            {
                fonts.Remove(identifier);
                font.Dispose();
            }
        });
    }

    #region IDisposable Support

    bool disposed;
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            fonts.Dispose();
            references.Clear();
            fonts = null;
            disposed = true;
        }
    }
    public void Dispose() => Dispose(true);

    #endregion
}