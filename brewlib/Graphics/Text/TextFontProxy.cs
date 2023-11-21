using System;

namespace BrewLib.Graphics.Text
{
    public class TextFontProxy(TextFont textFont, Action disposed) : TextFont
    {
        TextFont textFont = textFont;
        readonly Action disposed = disposed;

        public string Name => textFont.Name;
        public float Size => textFont.Size;
        public int LineHeight => textFont.LineHeight;

        public FontGlyph GetGlyph(char c) => textFont.GetGlyph(c);

        #region IDisposable Support

        bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing) disposed();
                textFont = null;
                disposedValue = true;
            }
        }
        public void Dispose() => Dispose(true);

        #endregion
    }
}