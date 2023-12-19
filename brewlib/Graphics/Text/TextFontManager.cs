using System;
using System.Collections.Generic;
using BrewLib.Util;

namespace BrewLib.Graphics.Text;

public class TextFontManager : IDisposable
{
    Dictionary<string, TextFontAtlased> fonts = [];
    Dictionary<string, int> references = [];

    public TextFont GetTextFont(string fontName, float fontSize, float scaling)
    {
        var identifier = $"{fontName}|{fontSize}|{scaling}";

        if (!fonts.TryGetValue(identifier, out var font)) fonts.Add(identifier, font = new(fontName, fontSize * scaling));
        if (references.TryGetValue(identifier, out var refCount)) references[identifier] = refCount + 1;
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

    ~TextFontManager() => Dispose(false);

    bool disposed;
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            fonts.Dispose();
            if (disposing)
            {
                references.Clear();

                fonts = null;
                references = null;
                disposed = true;
            }
        }
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}