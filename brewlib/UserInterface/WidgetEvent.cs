namespace BrewLib.UserInterface;

using System;

public sealed class WidgetEvent(Widget relatedTarget)
{
    public readonly Widget RelatedTarget = relatedTarget;
    public bool Handled;
    public Widget Listener;
}

public sealed class WidgetHoveredEventArgs(bool hovered) : EventArgs
{
    public readonly bool Hovered = hovered;
}

public sealed class WidgetFocusEventArgs(bool hasFocus) : EventArgs
{
    public readonly bool HasFocus = hasFocus;
}