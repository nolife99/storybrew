namespace BrewLib.Graphics.Text;

using System;

public interface TextFont : IDisposable
{
    string Name { get; }
    float Size { get; }
    int LineHeight { get; }

    FontGlyph GetGlyph(char c);
}