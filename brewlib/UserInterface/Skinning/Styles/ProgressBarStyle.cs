namespace BrewLib.UserInterface.Skinning.Styles;

using Graphics.Drawables;

public record ProgressBarStyle : WidgetStyle
{
    public Drawable Bar;
    public int Height;
}