using System;

namespace BrewLib.Graphics.Text;

public sealed class TextFontProxy(TextFont textFont, Action disposed) : TextFont
{
    public string Name => textFont.Name;
    public float Size => textFont.Size;
    public int LineHeight => textFont.LineHeight;

    public FontGlyph GetGlyph(char c) => textFont.GetGlyph(c);

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (!disposed)
        {
            disposed();
            textFont = null;
            disposed = true;
        }
    }

    #endregion
}