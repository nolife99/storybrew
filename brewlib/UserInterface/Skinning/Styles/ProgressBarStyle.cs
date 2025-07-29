namespace BrewLib.UserInterface.Skinning.Styles;

using BrewLib.Graphics.Drawables;

public record ProgressBarStyle : WidgetStyle
{
    public Drawable Bar;
    public int Height;
}