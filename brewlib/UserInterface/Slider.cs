namespace BrewLib.UserInterface;

using System;
using System.Numerics;
using BrewLib.UserInterface.Skinning.Styles;

public class Slider : ProgressBar
{
    bool disabled, hovered, dragged;
    byte dragButton;

    public double Step;

    public Slider(WidgetManager manager) : base(manager)
    {
        OnHovered += (_, e) =>
        {
            hovered = e.Hovered;
            if (!disabled) RefreshStyle();
        };

        OnClickDown += (_, e) =>
        {
            if (disabled || dragged) return false;

            dragButton = e.Button;
            dragged = true;
            Value = GetValueForPosition(manager.InputManager.MousePosition);
            DragStart(dragButton);
            return true;
        };

        OnClickUp += (_, e) =>
        {
            if (disabled || !dragged) return;
            if (e.Button != dragButton) return;

            dragged = false;
            RefreshStyle();
            DragEnd(dragButton);
            OnValueCommited?.Invoke(this, EventArgs.Empty);
        };

        OnClickMove += (_, e) =>
        {
            if (disabled || !dragged) return;

            Value = GetValueForPosition(new(e.X, e.Y));
            DragUpdate(dragButton);
        };
    }

    public bool Disabled
    {
        get => disabled;
        set
        {
            if (disabled == value) return;

            disabled = value;
            dragged = false;
            RefreshStyle();
        }
    }

    protected override WidgetStyle Style
        => Manager.Skin.GetStyle<ProgressBarStyle>(BuildStyleName(disabled ? "disabled" :
            dragged || hovered ? "hover" : null));

    public event EventHandler OnValueCommited;

    public double GetValueForPosition(Vector2 position)
    {
        var bounds = Bounds;
        var value = MinValue + (MaxValue - MinValue) * (Manager.Camera.FromScreen(position).X - bounds.Left) /
            bounds.Width;

        if (Step != 0) value = double.Min((int)(value / Step) * Step, MaxValue);
        return value;
    }

    protected virtual void DragStart(byte button) { }
    protected virtual void DragUpdate(byte button) { }
    protected virtual void DragEnd(byte button) { }
}