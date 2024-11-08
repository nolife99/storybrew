namespace BrewLib.UserInterface;

using System;
using System.Numerics;
using osuTK.Input;
using Skinning.Styles;

public class Slider : ProgressBar
{
    MouseButton dragButton;
    bool disabled, hovered, dragged;

    public float Step;

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
            Value = GetValueForPosition(new(e.X, e.Y));
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
            OnValueCommited?.Invoke(this, e);
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
        => Manager.Skin.GetStyle<ProgressBarStyle>(BuildStyleName(disabled ? "disabled" : dragged || hovered ? "hover" : null));

    public event EventHandler OnValueCommited;

    public float GetValueForPosition(Vector2 position)
    {
        var bounds = Bounds;
        var value = MinValue + (MaxValue - MinValue) * (Manager.Camera.FromScreen(position).X - bounds.Left) / bounds.Width;
        if (Step != 0) value = Math.Min((int)(value / Step) * Step, MaxValue);
        return value;
    }

    protected virtual void DragStart(MouseButton button) { }
    protected virtual void DragUpdate(MouseButton button) { }
    protected virtual void DragEnd(MouseButton button) { }
}