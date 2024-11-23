namespace BrewLib.UserInterface.Skinning.Styles;

using SixLabors.ImageSharp.PixelFormats;
using Util;

public record LabelStyle : WidgetStyle
{
    public Rgba32 Color;
    public string FontName;
    public float FontSize;
    public BoxAlignment TextAlignment;
}