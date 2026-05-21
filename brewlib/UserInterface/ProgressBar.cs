namespace BrewLib.UserInterface;

using System;
using System.Globalization;
using System.Numerics;
using Graphics;
using Graphics.Drawables;
using Skinning.Styles;

public class ProgressBar(WidgetManager manager) : Widget(manager), Field
{
    Drawable bar = NullDrawable.Instance;

    public double MinValue, MaxValue = 1;
    int preferredHeight = 32;

    double value = .5f;

    public override Vector2 MinSize => bar.MinSize;

    public override Vector2 PreferredSize
        => new(float.Max(200, bar.PreferredSize.X), float.Max(preferredHeight, bar.PreferredSize.Y));

    public double Value
    {
        get => value;
        set
        {
            value = double.Clamp(value, MinValue, MaxValue);

            if (this.value == value) return;

            this.value = value;
            OnValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override WidgetStyle Style => Manager.Skin.GetStyle<ProgressBarStyle>(StyleName);

    public object FieldValue { get => Value; set => Value = Convert.ToDouble(value, CultureInfo.InvariantCulture); }

    public event EventHandler OnValueChanged;

    public void SetValueSilent(double val) => value = double.Clamp(val, MinValue, MaxValue);

    protected override void Dispose(bool disposing)
    {
        bar = null;
        base.Dispose(disposing);
    }

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

        bar.Draw(drawContext,
            Manager.Camera,
            new(Bounds.X, Bounds.Y, minWidth + (Bounds.Width - minWidth) * (float)progress, Bounds.Height),
            actualOpacity);
    }
}