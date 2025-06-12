namespace BrewLib.Graphics.Text;

using System;

public sealed class TextFontProxy<TState>(TextFont textFont, Action<TextFont, TState> dispose, TState state) : TextFont
{
    public string Name => textFont.Name;
    public float Size => textFont.Size;
    public int LineHeight => textFont.LineHeight;

    public FontGlyph GetGlyph(char c) => textFont.GetGlyph(c);

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        dispose(textFont, state);
        disposed = true;
    }

    #endregion
}