using System;

namespace BrewLib.UserInterface;

public class WidgetEvent(Widget target, Widget relatedTarget)
{
    public readonly Widget Target = target, RelatedTarget = relatedTarget;
    public Widget Listener;
    public bool Handled;
}
public class WidgetHoveredEventArgs(bool hovered) : EventArgs
{
    public bool Hovered => hovered;
}
public class WidgetFocusEventArgs(bool hasFocus) : EventArgs
{
    public bool HasFocus => hasFocus;
}