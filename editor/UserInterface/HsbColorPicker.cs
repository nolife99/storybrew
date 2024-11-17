namespace StorybrewEditor.UserInterface;

using System;
using SixLabors.ImageSharp;
using System.Numerics;
using System.Runtime.CompilerServices;
using BrewLib.Graphics;
using BrewLib.Graphics.Drawables;
using BrewLib.UserInterface;
using BrewLib.UserInterface.Skinning.Styles;
using BrewLib.Util;
using SixLabors.ImageSharp.PixelFormats;
using Skinning.Styles;
using StorybrewCommon.Subtitles;

public class HsbColorPicker : Widget, Field
{
    const float previewHeight = 24;
    readonly Textbox htmlTextbox;
    readonly Slider hueSlider, saturationSlider, brightnessSlider, alphaSlider;
    readonly LinearLayout layout;
    Sprite previewSprite;

    Rgba32 value;

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
                hueSlider = new(manager) { StyleName = "small", MinValue = 0, MaxValue = 1, Value = 0 },
                new Label(manager) { StyleName = "small", Text = "Saturation" },
                saturationSlider = new(manager) { StyleName = "small", MinValue = 0, MaxValue = 1, Value = .7f },
                new Label(manager) { StyleName = "small", Text = "Brightness" },
                brightnessSlider = new(manager) { StyleName = "small", MinValue = 0, MaxValue = 1, Value = 1 },
                new Label(manager) { StyleName = "small", Text = "Alpha" },
                alphaSlider = new(manager) { StyleName = "small", MinValue = 0, MaxValue = 1, Value = 1 },
                htmlTextbox = new(manager) { EnterCommits = true }
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
        get => value;
        set
        {
            if (this.value == value) return;
            this.value = value;

            updateWidgets();
            OnValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override WidgetStyle Style => Manager.Skin.GetStyle<ColorPickerStyle>(BuildStyleName());

    public object FieldValue { get => Value; set => Value = Unsafe.Unbox<Rgba32>(value); }

    public event EventHandler OnValueChanged, OnValueCommited;

    void slider_OnValueChanged(object sender, EventArgs e) => Value = ColorExtensions.FromHsb(new(hueSlider.Value % 1,
        saturationSlider.Value, brightnessSlider.Value, alphaSlider.Value));

    void slider_OnValueCommited(object sender, EventArgs e) => OnValueCommited?.Invoke(this, EventArgs.Empty);

    void htmlTextbox_OnValueCommited(object sender, EventArgs e)
    {
        var htmlColor = htmlTextbox.Value.Trim();
        if (!htmlColor.StartsWith('#')) htmlColor = '#' + htmlColor;

        Rgba32 color;
        try
        {
            color = Rgba32.ParseHex(htmlColor);
        }
        catch
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

        previewSprite.Color = value;
        htmlTextbox.SetValueSilent(previewSprite.Color.ToHex());
    }

    protected override void DrawBackground(DrawContext drawContext, float actualOpacity)
    {
        base.DrawBackground(drawContext, actualOpacity);

        var bounds = Bounds;
        previewSprite.Draw(drawContext, Manager.Camera,
            RectangleF.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Top + previewHeight), actualOpacity);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) previewSprite.Dispose();
        previewSprite = null;
        base.Dispose(disposing);
    }

    protected override void Layout()
    {
        base.Layout();
        layout.Offset = new(0, previewHeight);
        layout.Size = new(Size.X, Size.Y - previewHeight);
    }
}