namespace BrewLib.UserInterface.Skinning.Styles;

using SixLabors.ImageSharp;
using Util;

public record LabelStyle : WidgetStyle
{
    public Color Color;
    public string FontName;
    public float FontSize;
    public BoxAlignment TextAlignment;
}