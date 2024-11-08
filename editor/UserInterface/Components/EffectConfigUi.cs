namespace StorybrewEditor.UserInterface.Components;

using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BrewLib.UserInterface;
using BrewLib.Util;
using osuTK.Graphics;
using Storyboarding;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Subtitles;
using StorybrewCommon.Util;

public class EffectConfigUi : Widget
{
    const string effectConfigFormat = "storybrewEffectConfig";
    readonly LinearLayout layout, configFieldsLayout;

    readonly Label titleLabel;

    Effect effect;

    public EffectConfigUi(WidgetManager manager) : base(manager)
    {
        Button copyButton, pasteButton, closeButton;

        Add(layout = new(manager)
        {
            StyleName = "panel",
            Padding = new(16),
            FitChildren = true,
            Fill = true,
            Children =
            [
                new LinearLayout(manager)
                {
                    Fill = true,
                    FitChildren = true,
                    Horizontal = true,
                    CanGrow = false,
                    Children =
                    [
                        titleLabel = new(manager) { Text = "Configuration" },
                        copyButton = new(Manager)
                        {
                            StyleName = "icon",
                            Icon = IconFont.CopyAll,
                            Tooltip = "Copy all fields",
                            AnchorFrom = BoxAlignment.Centre,
                            AnchorTo = BoxAlignment.Centre,
                            CanGrow = false
                        },
                        pasteButton = new(Manager)
                        {
                            StyleName = "icon",
                            Icon = IconFont.ContentPasteGo,
                            Tooltip = "Paste all fields",
                            AnchorFrom = BoxAlignment.Centre,
                            AnchorTo = BoxAlignment.Centre,
                            CanGrow = false
                        },
                        closeButton = new(Manager)
                        {
                            StyleName = "icon",
                            Icon = IconFont.Cancel,
                            AnchorFrom = BoxAlignment.Centre,
                            AnchorTo = BoxAlignment.Centre,
                            CanGrow = false
                        }
                    ]
                },
                new ScrollArea(manager, configFieldsLayout = new(manager) { FitChildren = true })
            ]
        });

        copyButton.OnClick += (_, _) => copyConfiguration();
        pasteButton.OnClick += (_, _) => pasteConfiguration();
        closeButton.OnClick += (_, _) =>
        {
            Effect = null;
            Displayed = false;
        };
    }

    public override Vector2 MinSize => layout.MinSize;
    public override Vector2 MaxSize => layout.MaxSize;
    public override Vector2 PreferredSize => layout.PreferredSize;

    public Effect Effect
    {
        get => effect;
        set
        {
            if (effect == value) return;
            if (effect is not null)
            {
                effect.OnChanged -= Effect_OnChanged;
                effect.OnConfigFieldsChanged -= Effect_OnConfigFieldsChanged;
            }

            effect = value;
            if (effect is not null)
            {
                effect.OnChanged += Effect_OnChanged;
                effect.OnConfigFieldsChanged += Effect_OnConfigFieldsChanged;
            }

            updateEffect();
            updateFields();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Effect = null;
        base.Dispose(disposing);
    }

    protected override void Layout()
    {
        base.Layout();
        layout.Size = Size;
    }

    void Effect_OnChanged(object sender, EventArgs e) => updateEffect();
    void Effect_OnConfigFieldsChanged(object sender, EventArgs e) => updateFields();

    void updateEffect()
    {
        if (effect is null) return;
        titleLabel.Text = $"Configuration: {effect.Name} ({effect.BaseName})";
    }

    void updateFields()
    {
        configFieldsLayout.ClearWidgets();
        if (effect is null) return;

        var currentGroup = (string)null;
        foreach (var field in effect.Config.SortedFields)
        {
            if (!string.IsNullOrWhiteSpace(field.BeginsGroup))
            {
                currentGroup = field.BeginsGroup;
                configFieldsLayout.Add(new Label(Manager)
                {
                    StyleName = "listGroup",
                    Text = field.BeginsGroup,
                    AnchorFrom = BoxAlignment.Centre,
                    AnchorTo = BoxAlignment.Centre
                });
            }

            var displayName = field.DisplayName;
            if (currentGroup is not null)
                displayName = Regex.Replace(displayName, $@"^{Regex.Escape(currentGroup)}\s+", "");

            var description = $"Variable: {field.Name} ({field.Type.Name})";
            if (!string.IsNullOrWhiteSpace(field.Description))
                description = "  " + description + "\n" + field.Description;

            configFieldsLayout.Add(new LinearLayout(Manager)
            {
                AnchorFrom = BoxAlignment.Centre,
                AnchorTo = BoxAlignment.Centre,
                Horizontal = true,
                Fill = true,
                Children =
                [
                    new Label(Manager)
                    {
                        StyleName = "listItem",
                        Text = displayName,
                        AnchorFrom = BoxAlignment.TopLeft,
                        AnchorTo = BoxAlignment.TopLeft,
                        Tooltip = description
                    },
                    buildFieldEditor(field)
                ]
            });
        }
    }

    Widget buildFieldEditor(EffectConfig.ConfigField field)
    {
        if (field.AllowedValues is not null)
        {
            Selectbox widget = new(Manager)
            {
                Value = field.Value,
                Options = field.AllowedValues,
                AnchorFrom = BoxAlignment.Right,
                AnchorTo = BoxAlignment.Right,
                CanGrow = false
            };
            widget.OnValueChanged += (_, _) => setFieldValue(field, widget.Value);
            return widget;
        }

        if (field.Type == typeof(bool))
        {
            Selectbox widget = new(Manager)
            {
                Value = field.Value,
                Options =
                [
                    new() { Name = bool.TrueString, Value = true },
                    new() { Name = bool.FalseString, Value = false }
                ],
                AnchorFrom = BoxAlignment.Right,
                AnchorTo = BoxAlignment.Right,
                CanGrow = false
            };
            widget.OnValueChanged += (_, _) => setFieldValue(field, widget.Value);
            return widget;
        }

        if (field.Type == typeof(string))
        {
            Textbox widget = new(Manager)
            {
                Value = field.Value?.ToString(),
                AnchorFrom = BoxAlignment.Right,
                AnchorTo = BoxAlignment.Right,
                AcceptMultiline = true,
                CanGrow = false
            };
            widget.OnValueCommited += (_, _) =>
            {
                setFieldValue(field, widget.Value);
                widget.Value = effect.Config.GetValue(field.Name).ToString();
            };
            return widget;
        }

        if (field.Type == typeof(Vector2) || field.Type == typeof(osuTK.Vector2) ||
            field.Type == typeof(CommandPosition) || field.Type == typeof(CommandScale))
            return vector2Field(field);
        if (field.Type == typeof(Vector3) || field.Type == typeof(osuTK.Vector3))
        {
            var x = field.Type.GetField("X");
            var y = field.Type.GetField("Y");
            var z = field.Type.GetField("Z");

            Vector3Picker widget = new(Manager)
            {
                Value =
                [
                    Unsafe.Unbox<float>(x.GetValue(field.Value)), Unsafe.Unbox<float>(y.GetValue(field.Value)),
                    Unsafe.Unbox<float>(z.GetValue(field.Value))
                ],
                AnchorFrom = BoxAlignment.Right,
                AnchorTo = BoxAlignment.Right,
                CanGrow = false
            };
            widget.OnValueCommited += (_, _) =>
            {
                var ctor = field.Type.GetConstructor([typeof(float), typeof(float), typeof(float)]);
                setFieldValue(field, ctor.Invoke([widget.Value[0], widget.Value[1], widget.Value[2]]));

                var configVal = effect.Config.GetValue(field.Name);
                widget.Value =
                [
                    Unsafe.Unbox<float>(x.GetValue(configVal)), Unsafe.Unbox<float>(y.GetValue(configVal)),
                    Unsafe.Unbox<float>(z.GetValue(configVal))
                ];
            };
        }
        else if (field.Type == typeof(CommandColor) || field.Type == typeof(Color4) || field.Type == typeof(Color) ||
            field.Type == typeof(FontColor))
            return colorField(field);
        else if (field.Type.GetInterface(nameof(IConvertible)) is not null)
        {
            Textbox widget = new(Manager)
            {
                Value = Convert.ToString(field.Value, CultureInfo.InvariantCulture),
                AnchorFrom = BoxAlignment.Right,
                AnchorTo = BoxAlignment.Right,
                CanGrow = false
            };
            widget.OnValueCommited += (_, _) =>
            {
                try
                {
                    var value = Convert.ChangeType(widget.Value, field.Type, CultureInfo.InvariantCulture);
                    setFieldValue(field, value);
                }
                catch
                {
                    // ignored
                }

                widget.Value = Convert.ToString(effect.Config.GetValue(field.Name), CultureInfo.InvariantCulture);
            };
            return widget;
        }

        return new Label(Manager)
        {
            StyleName = "listItem",
            Text = field.Value.ToString(),
            Tooltip = $"Values of type {field.Type.Name} cannot be edited",
            AnchorFrom = BoxAlignment.Right,
            AnchorTo = BoxAlignment.Right,
            CanGrow = false
        };
    }

    Vector2Picker vector2Field(EffectConfig.ConfigField field)
    {
        if (field.Type == typeof(Vector2) || field.Type == typeof(osuTK.Vector2))
        {
            Vector2Picker widget = new(Manager)
            {
                Value = field.Type == typeof(Vector2)
                    ? Unsafe.As<Vector2, CommandPosition>(ref Unsafe.Unbox<Vector2>(field.Value))
                    : Unsafe.As<osuTK.Vector2, CommandPosition>(ref Unsafe.Unbox<osuTK.Vector2>(field.Value)),
                AnchorFrom = BoxAlignment.Right,
                AnchorTo = BoxAlignment.Right,
                CanGrow = false
            };
            widget.OnValueCommited += (_, _) =>
            {
                if (field.Type == typeof(Vector2))
                    setFieldValue(field, (Vector2)widget.Value);
                else
                    setFieldValue(field, (osuTK.Vector2)widget.Value);
                widget.Value = field.Type == typeof(Vector2)
                    ? Unsafe.As<Vector2, CommandPosition>(ref Unsafe.Unbox<Vector2>(effect.Config.GetValue(field.Name)))
                    : Unsafe.As<osuTK.Vector2, CommandPosition>(
                        ref Unsafe.Unbox<osuTK.Vector2>(effect.Config.GetValue(field.Name)));
            };
            return widget;
        }

        {
            Vector2Picker widget = new(Manager)
            {
                Value = field.Type == typeof(CommandPosition) ? Unsafe.Unbox<CommandPosition>(field.Value)
                    : Unsafe.As<CommandScale, CommandPosition>(ref Unsafe.Unbox<CommandScale>(field.Value)),
                AnchorFrom = BoxAlignment.Right,
                AnchorTo = BoxAlignment.Right,
                CanGrow = false
            };
            widget.OnValueCommited += (_, _) =>
            {
                if (field.Type == typeof(CommandPosition))
                    setFieldValue(field, widget.Value);
                else
                    setFieldValue(field, (CommandScale)widget.Value);
                widget.Value = field.Type == typeof(Vector2) ? Unsafe.Unbox<Vector2>(effect.Config.GetValue(field.Name))
                    : Unsafe.As<CommandScale, CommandPosition>(
                        ref Unsafe.Unbox<CommandScale>(effect.Config.GetValue(field.Name)));
            };
            return widget;
        }
    }

    HsbColorPicker colorField(EffectConfig.ConfigField field)
    {
        if (field.Type == typeof(Color4) || field.Type == typeof(Color))
        {
            HsbColorPicker widget = new(Manager)
            {
                Value = field.Type == typeof(Color4) ? Unsafe.Unbox<Color4>(field.Value)
                    : (FontColor)Unsafe.Unbox<Color>(field.Value),
                AnchorFrom = BoxAlignment.Right,
                AnchorTo = BoxAlignment.Right,
                CanGrow = false
            };
            widget.OnValueCommited += (_, _) =>
            {
                if (field.Type == typeof(Color4))
                    setFieldValue(field, (Color4)widget.Value);
                else
                    setFieldValue(field, (Color)widget.Value);
                widget.Value = field.Type == typeof(Color4) ? Unsafe.Unbox<Color4>(effect.Config.GetValue(field.Name))
                    : (FontColor)Unsafe.Unbox<Color>(effect.Config.GetValue(field.Name));
            };
            return widget;
        }

        {
            HsbColorPicker widget = new(Manager)
            {
                Value = field.Type == typeof(FontColor) ? Unsafe.Unbox<FontColor>(field.Value)
                    : Unsafe.Unbox<CommandColor>(field.Value),
                AnchorFrom = BoxAlignment.Right,
                AnchorTo = BoxAlignment.Right,
                CanGrow = false
            };
            widget.OnValueCommited += (_, _) =>
            {
                if (field.Type == typeof(FontColor))
                    setFieldValue(field, widget.Value);
                else
                    setFieldValue(field, (CommandColor)widget.Value);
                widget.Value = field.Type == typeof(FontColor)
                    ? Unsafe.Unbox<FontColor>(effect.Config.GetValue(field.Name))
                    : Unsafe.Unbox<CommandColor>(effect.Config.GetValue(field.Name));
            };
            return widget;
        }
    }

    void setFieldValue(EffectConfig.ConfigField field, object value)
    {
        if (effect.Config.SetValue(field.Name, value)) effect.Refresh();
    }

    void copyConfiguration()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(effect.Config.FieldCount);

        foreach (var field in effect.Config.Fields)
        {
            writer.Write(field.Name);
            ObjectSerializer.Write(writer, field.Value);
            ClipboardHelper.SetData(effectConfigFormat, stream);
        }
    }

    void pasteConfiguration()
    {
        var changed = false;
        try
        {
            using var stream = (Stream)ClipboardHelper.GetData(effectConfigFormat);
            using BinaryReader reader = new(stream);

            var fieldCount = reader.ReadInt32();
            for (var i = 0; i < fieldCount; ++i)
            {
                var name = reader.ReadString();
                var value = ObjectSerializer.Read(reader);
                try
                {
                    var field = effect.Config.Fields.First(f => f.Name == name);
                    if (field.Value.Equals(value)) continue;

                    changed |= effect.Config.SetValue(name, value);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Cannot paste '{name}': {ex}");
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Cannot paste clipboard data: {ex}");
        }

        if (!changed) return;

        updateFields();
        effect.Refresh();
    }
}