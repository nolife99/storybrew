namespace BrewLib.UserInterface;

using System;
using Input;
using SDL3;

public sealed class ClickBehavior : IDisposable
{
    readonly Widget widget;
    bool disabled, hovered, pressed;

    byte pressedButton;

    public ClickBehavior(Widget widget)
    {
        InputManager.SetCursor(true, SDL.SystemCursor.Default);

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
    public event EventHandler<SDL.MouseButtonEvent> OnClick;

    void widget_OnHovered(WidgetEvent evt, WidgetHoveredEventArgs e)
    {
        if (hovered == e.Hovered) return;

        hovered = e.Hovered;
        if (!disabled) OnStateChanged?.Invoke(this, EventArgs.Empty);

        InputManager.SetCursor(hovered, SDL.SystemCursor.Pointer);
    }

    bool widget_OnClickDown(WidgetEvent evt, SDL.MouseButtonEvent e)
    {
        if (pressed || disabled) return false;

        pressed = true;
        pressedButton = e.Button;
        OnStateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    void widget_OnClickUp(WidgetEvent evt, SDL.MouseButtonEvent e)
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

        InputManager.SetCursor(hovered, SDL.SystemCursor.Default);

        disposed = true;
    }

    #endregion
}