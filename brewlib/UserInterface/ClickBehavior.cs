﻿namespace BrewLib.UserInterface;

using System;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

public sealed class ClickBehavior : IDisposable
{
    readonly Widget widget;
    bool disabled, hovered, pressed;

    MouseButton pressedButton;

    public ClickBehavior(Widget widget)
    {
        this.widget = widget;

        widget.OnHovered += widget_OnHovered;
        widget.OnClickDown += widget_OnClickDown;
        widget.OnClickUp += widget_OnClickUp;
    }

    public bool Hovered => !disabled && hovered;
    public bool Pressed => !disabled && pressed;

    public bool Disabled
    {
        get => disabled;
        set
        {
            if (disabled == value) return;

            pressed = false;
            disabled = value;
            OnStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler OnStateChanged;
    public event EventHandler<MouseButtonEventArgs> OnClick;

    void widget_OnHovered(WidgetEvent evt, WidgetHoveredEventArgs e)
    {
        if (hovered == e.Hovered) return;

        hovered = e.Hovered;
        if (!disabled) OnStateChanged?.Invoke(this, e);
    }

    bool widget_OnClickDown(WidgetEvent evt, MouseButtonEventArgs e)
    {
        if (pressed || disabled) return false;

        pressed = true;
        pressedButton = e.Button;
        OnStateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    void widget_OnClickUp(WidgetEvent evt, MouseButtonEventArgs e)
    {
        if (!pressed || disabled) return;
        if (e.Button != pressedButton) return;

        pressed = false;
        if (hovered) OnClick?.Invoke(this, e);
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        widget.OnHovered -= widget_OnHovered;
        widget.OnClickDown -= widget_OnClickDown;
        widget.OnClickUp -= widget_OnClickUp;

        disposed = true;
    }

    #endregion
}