using System;
using System.Globalization;
using System.Numerics;
using BrewLib.Graphics;
using BrewLib.Graphics.Drawables;
using BrewLib.UserInterface.Skinning.Styles;

namespace BrewLib.UserInterface;

public class ProgressBar(WidgetManager manager) : Widget(manager), Field
{
    Drawable bar = NullDrawable.Instance;
    int preferredHeight = 32;

    public override Vector2 MinSize => bar.MinSize;
    public override Vector2 PreferredSize => new(Math.Max(200, bar.PreferredSize.X), Math.Max(preferredHeight, bar.PreferredSize.Y));

    public float MinValue, MaxValue = 1;

    float value = .5f;
    public float Value
    {
        get => value;
        set
        {
            value = Math.Clamp(value, MinValue, MaxValue);

            if (this.value == value) return;
            this.value = value;
            OnValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public object FieldValue
    {
        get => Value;
        set => Value = (float)Convert.ChangeType(value, typeof(float), CultureInfo.InvariantCulture);
    }

    public void SetValueSilent(float value) => this.value = Math.Min(Math.Max(MinValue, value), MaxValue);
    public event EventHandler OnValueChanged;

    protected override void Dispose(bool disposing)
    {
        bar = null;
        base.Dispose(disposing);
    }

    protected override WidgetStyle Style => Manager.Skin.GetStyle<ProgressBarStyle>(StyleName);
    protected override void ApplyStyle(WidgetStyle style)
    {
        base.ApplyStyle(style);
        var progressBarStyle = (ProgressBarStyle)style;

        bar = progressBarStyle.Bar;
        preferredHeight = progressBarStyle.Height;
    }
    protected override void DrawBackground(DrawContext drawContext, float actualOpacity)
    {
        base.DrawBackground(drawContext, actualOpacity);

        var progress = (value - MinValue) / (MaxValue - MinValue);
        var minWidth = bar.MinSize.X;

        bar.Draw(drawContext, Manager.Camera, new(Bounds.X, Bounds.Y, minWidth + (Bounds.Width - minWidth) * progress, Bounds.Height), actualOpacity);
    }
}