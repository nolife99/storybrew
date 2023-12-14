using System.Drawing;
using BrewLib.Util;

namespace BrewLib.UserInterface.Skinning.Styles;

public class LabelStyle : WidgetStyle
{
    public string FontName;
    public float FontSize;
    public BoxAlignment TextAlignment;
    public StringTrimming Trimming;
    public Color Color;
}