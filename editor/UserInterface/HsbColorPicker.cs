namespace StorybrewEditor.UserInterface;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using BrewLib.Graphics;
using BrewLib.Graphics.Drawables;
using BrewLib.UserInterface;
using BrewLib.UserInterface.Skinning.Styles;
using BrewLib.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Skinning.Styles;

public class HsbColorPicker : Widget, Field
{
    const float previewHeight = 24;
    readonly Textbox htmlTextbox;
    readonly Slider hueSlider, saturationSlider, brightnessSlider, alphaSlider;
    readonly LinearLayout layout;
    readonly Sprite previewSprite;

    Vector4 value;

    public HsbColorPicker(WidgetManager manager) : base(manager)
    {
        previewSprite = new() { Texture = DrawState.WhitePixel, ScaleMode = ScaleMode.Fill };

        Add(layout = new(manager)
        {
            StyleName = "condensed",
            FitChildren = true,
            Children =
            [
                new Label(manager) { StyleName = "small", Text = "Hue" },
                hueSlider = new(manager) { StyleName = "small", Value = 0 },
                new Label(manager) { StyleName = "small", Text = "Saturation" },
                saturationSlider = new(manager) { StyleName = "small", Value = .7f },
                new Label(manager) { StyleName = "small", Text = "Brightness" },
                brightnessSlider = new(manager) { StyleName = "small", Value = 1 },
                new Label(manager) { StyleName = "small", Text = "Alpha" },
                alphaSlider = new(manager) { StyleName = "small", Value = 1 },
                htmlTextbox = new(manager)
            ]
        });

        hueSlider.OnValueChanged += slider_OnValueChanged;
        saturationSlider.OnValueChanged += slider_OnValueChanged;
        brightnessSlider.OnValueChanged += slider_OnValueChanged;
        alphaSlider.OnValueChanged += slider_OnValueChanged;

        hueSlider.OnValueCommited += slider_OnValueCommited;
        saturationSlider.OnValueCommited += slider_OnValueCommited;
        brightnessSlider.OnValueCommited += slider_OnValueCommited;
        alphaSlider.OnValueCommited += slider_OnValueCommited;
        htmlTextbox.OnValueCommited += htmlTextbox_OnValueCommited;

        updateWidgets();
    }

    public override Vector2 MinSize => layout.MinSize with { Y = layout.MinSize.Y + previewHeight };

    public override Vector2 MaxSize => Vector2.Zero;

    public override Vector2 PreferredSize => layout.PreferredSize with { Y = layout.PreferredSize.Y + previewHeight };

    public Rgba32 Value
    {
        get => new(value);
        set
        {
            if (new Rgba32(this.value) == value) return;

            this.value = value.ToVector4();

            updateWidgets();
            OnValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override WidgetStyle Style => Manager.Skin.GetStyle<ColorPickerStyle>(BuildStyleName());

    public object FieldValue { get => Value; set => Value = Unsafe.Unbox<Rgba32>(value); }

    public event EventHandler OnValueChanged, OnValueCommited;

    void slider_OnValueChanged(object sender, EventArgs e)
    {
        var value = ColorExtensions.FromHsb(new(hueSlider.Value % 1,
            saturationSlider.Value,
            brightnessSlider.Value,
            alphaSlider.Value));

        if (this.value == value) return;

        this.value = value;

        updateWidgets();
        OnValueChanged?.Invoke(this, EventArgs.Empty);
    }

    void slider_OnValueCommited(object sender, EventArgs e) => OnValueCommited?.Invoke(this, EventArgs.Empty);

    void htmlTextbox_OnValueCommited(object sender, EventArgs e)
    {
        var success = Rgba32.TryParseHex(htmlTextbox.Value, out var color);
        if (!success)
        {
            updateWidgets();
            return;
        }

        Value = new(color.R / 255f, color.G / 255f, color.B / 255f, alphaSlider.Value);
        OnValueCommited?.Invoke(this, EventArgs.Empty);
    }

    void updateWidgets()
    {
        var hsba = ColorExtensions.ToHsb(value);
        if (hsba.Z > 0)
        {
            if (!float.IsNaN(hsba.X))
            {
                hueSlider.SetValueSilent(hsba.X);
                hueSlider.Tooltip = $"{hsba.X * 360:F0}°";
                hueSlider.Disabled = false;
            }
            else
            {
                hueSlider.Tooltip = null;
                hueSlider.Disabled = true;
            }

            saturationSlider.SetValueSilent(hsba.Y);
            saturationSlider.Tooltip = $"{hsba.Y:.%}";
            saturationSlider.Disabled = false;
        }
        else
        {
            hueSlider.Tooltip = null;
            hueSlider.Disabled = true;

            saturationSlider.Tooltip = null;
            saturationSlider.Disabled = true;
        }

        brightnessSlider.SetValueSilent(hsba.Z);
        brightnessSlider.Tooltip = $"{hsba.Z:.%}";

        alphaSlider.SetValueSilent(hsba.W);
        alphaSlider.Tooltip = $"{hsba.W:.%}";

        Rgba32 bit32 = new(value);
        previewSprite.Color = bit32;
        htmlTextbox.SetValueSilent($"#{bit32.R:X2}{bit32.G:X2}{bit32.B:X2}");
    }

    protected override void DrawBackground(DrawContext drawContext, float actualOpacity)
    {
        base.DrawBackground(drawContext, actualOpacity);

        var bounds = Bounds;
        previewSprite.Draw(drawContext,
            Manager.Camera,
            RectangleF.FromLTRB(bounds.X, bounds.Y, bounds.Right, bounds.Y + previewHeight),
            actualOpacity);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) previewSprite.Dispose();
        base.Dispose(disposing);
    }

    protected override void Layout()
    {
        base.Layout();
        layout.Offset = new(0, previewHeight);
        layout.Size = new(Size.X, Size.Y - previewHeight);
    }
}