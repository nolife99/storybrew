namespace BrewLib.Graphics.Text;

using System;
using System.Collections.Generic;
using Util;

public class TextFontManager : IDisposable
{
    Dictionary<int, int> references = [];
    Dictionary<int, TextFontAtlased> fonts = [];

    public TextFont GetTextFont(string fontName, float fontSize, float scaling)
    {
        var identifier = HashCode.Combine(fontName, fontSize, scaling);
        if (!fonts.TryGetValue(identifier, out var font)) fonts.Add(identifier, font = new(fontName, fontSize * scaling));
        if (references.TryGetValue(identifier, out var refCount)) references[identifier] = refCount + 1;
        else references[identifier] = 1;

        return new TextFontProxy(font, () =>
        {
            if (--references[identifier] != 0) return;
            fonts.Remove(identifier);
            font.Dispose();
        });
    }

#region IDisposable Support

    bool disposed;
    protected virtual void Dispose(bool disposing)
    {
        if (disposed || !disposing) return;

        references.Clear();
        references = null;

        fonts.Dispose();
        fonts = null;
                
        disposed = true;
    }
    public void Dispose() 
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

#endregion
}