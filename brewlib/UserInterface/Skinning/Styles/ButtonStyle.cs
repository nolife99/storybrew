namespace BrewLib.UserInterface.Skinning.Styles;

using System.Numerics;
using BrewLib.Util;

public record ButtonStyle : WidgetStyle
{
    public Vector2 LabelOffset;
    public string LabelStyle;
    public FourSide Padding;
}