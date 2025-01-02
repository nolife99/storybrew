namespace BrewLib.UserInterface;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Skinning.Styles;
using Util;

public class Button : Widget, Field
{
    readonly ClickBehavior clickBehavior;
    readonly Label label;

    bool isCheckable, isChecked;
    FourSide padding;

    public Button(WidgetManager manager) : base(manager)
    {
        Add(label = new(manager) { AnchorFrom = BoxAlignment.Centre, AnchorTo = BoxAlignment.Centre, Hoverable = false });

        clickBehavior = new(this);
        clickBehavior.OnStateChanged += (_, _) => RefreshStyle();
        clickBehavior.OnClick += (_, e) => Click(e.Button);
    }

    public override Vector2 MinSize => new(label.MinSize.X + padding.Horizontal, label.MinSize.Y + padding.Vertical);

    public override Vector2 PreferredSize => new(
        label.PreferredSize.X + padding.Horizontal,
        label.PreferredSize.Y + padding.Vertical);

    public string Text { get => label.Text; set => label.Text = value; }

    public IconFont Icon { get => label.Icon; set => label.Icon = value; }

    public FourSide Padding
    {
        get => padding;
        set
        {
            if (padding == value) return;

            padding = value;
            InvalidateAncestorLayout();
        }
    }

    public bool Checkable
    {
        get => isCheckable;
        set
        {
            if (isCheckable == value) return;

            isCheckable = value;
            if (!isCheckable) Checked = false;
        }
    }

    public bool Checked
    {
        get => isChecked;
        set
        {
            if (isChecked == value) return;

            isChecked = value;
            RefreshStyle();
            OnValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool Disabled { get => clickBehavior.Disabled; set => clickBehavior.Disabled = value; }

    protected override WidgetStyle Style => Manager.Skin.GetStyle<ButtonStyle>(BuildStyleName(
        clickBehavior.Disabled ? "disabled" : null,
        clickBehavior.Hovered ? "hover" : null,
        clickBehavior.Pressed || isChecked ? "pressed" : null));

    public object FieldValue { get => Checked; set => Checked = Unsafe.Unbox<bool>(value); }

    public event EventHandler OnValueChanged;
    public event EventHandler<MouseButton> OnClick;

    public void Click(MouseButton button = MouseButton.Left)
    {
        if (isCheckable && button is MouseButton.Left) Checked = !Checked;
        OnClick?.Invoke(this, button);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) clickBehavior.Dispose();
        base.Dispose(disposing);
    }

    protected override void ApplyStyle(WidgetStyle style)
    {
        base.ApplyStyle(style);
        var buttonStyle = (ButtonStyle)style;

        Padding = buttonStyle.Padding;
        label.StyleName = buttonStyle.LabelStyle;
        label.Offset = buttonStyle.LabelOffset;
    }

    protected override void Layout()
    {
        base.Layout();
        label.Size = new(Size.X - padding.Horizontal, Size.Y - padding.Vertical);
    }
}