namespace BrewLib.UserInterface.Skinning.Styles;

using BrewLib.Util;
using SixLabors.ImageSharp;

public record ImageStyle : WidgetStyle
{
    public Color Color;
    public ScaleMode ScaleMode;
}