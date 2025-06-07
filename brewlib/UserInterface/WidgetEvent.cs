namespace BrewLib.UserInterface;

public sealed class WidgetEvent(Widget relatedTarget)
{
    public bool Handled;
    public Widget Listener;
    public Widget RelatedTarget => relatedTarget;
}

public readonly struct WidgetHoveredEventArgs(bool hovered)
{
    public bool Hovered => hovered;
}

public readonly struct WidgetFocusEventArgs(bool hasFocus)
{
    public bool HasFocus => hasFocus;
}