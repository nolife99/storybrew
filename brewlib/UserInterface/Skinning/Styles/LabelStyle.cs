namespace BrewLib.UserInterface.Skinning.Styles;

using BrewLib.Util;
using SixLabors.ImageSharp;

public record LabelStyle : WidgetStyle
{
    public Color Color;
    public string FontName;
    public float FontSize;
    public BoxAlignment TextAlignment;
}