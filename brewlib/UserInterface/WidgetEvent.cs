namespace BrewLib.UserInterface;

using System;

public class WidgetEvent(Widget relatedTarget)
{
    public readonly Widget RelatedTarget = relatedTarget;
    public bool Handled;
    public Widget Listener;
}

public class WidgetHoveredEventArgs(bool hovered) : EventArgs
{
    public bool Hovered => hovered;
}

public class WidgetFocusEventArgs(bool hasFocus) : EventArgs
{
    public bool HasFocus => hasFocus;
}