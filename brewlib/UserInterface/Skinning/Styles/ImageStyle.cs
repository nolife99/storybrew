namespace BrewLib.UserInterface.Skinning.Styles;

using SixLabors.ImageSharp;
using Util;

public record ImageStyle : WidgetStyle
{
    public Color Color;
    public ScaleMode ScaleMode;
}