namespace StorybrewEditor.UserInterface.Components;

using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using BrewLib.Memory;
using BrewLib.UserInterface;
using BrewLib.Util;
using SDL3;
using SixLabors.ImageSharp;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Util;
using StorybrewEditor.Storyboarding;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public class EffectConfigUi : Widget
{
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
                effect.Changed -= EffectChanged;
                effect.ConfigFieldsChanged -= EffectConfigFieldsChanged;
            }

            effect = value;
            if (effect is not null)
            {
                effect.Changed += EffectChanged;
                effect.ConfigFieldsChanged += EffectConfigFieldsChanged;
            }

            EffectChanged(effect);
            updateFields();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            (ClipboardHelper.GetData() as IDisposable)?.Dispose();
            Effect = null;
        }

        base.Dispose(disposing);
    }

    protected override void Layout()
    {
        base.Layout();
        layout.Size = Size;
    }

    void EffectChanged(Effect sender)
    {
        if (sender is null) return;

        using var text = StringHelper.Interpolate($"Configuration: {sender.Name} ({sender.BaseName})");
        titleLabel.Text = text.AsReadOnlySpan();
    }

    void EffectConfigFieldsChanged(Effect sender) => updateFields();

    void updateFields()
    {
        configFieldsLayout.ClearWidgets();
        if (effect is null) return;

        string currentGroup = null;
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

            using var description = StringHelper.Interpolate($"Variable: {field.Name} ({field.Type.Name})");
            if (!string.IsNullOrWhiteSpace(field.Description))
            {
                description.InsertRange(0, "  ");
                description.Append($"\n{field.Description}");
            }

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
                        Tooltip = description.AsReadOnlySpan()
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
                Options = [new(bool.TrueString, true), new(bool.FalseString, false)],
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
                setFieldValue(field, widget.Value.ToString());
                widget.Value = effect.Config.GetValue(field.Name).ToString();
            };

            return widget;
        }

        if (field.Type == typeof(Vector2) || field.Type == typeof(CommandPosition) ||
            field.Type == typeof(CommandScale)) return vector2Field(field);

        if (field.Type == typeof(Vector3))
        {
            var x = field.Type.GetField("X");
            var y = field.Type.GetField("Y");
            var z = field.Type.GetField("Z");

            Vector3Picker widget = new(Manager)
            {
                Value =
                [
                    (float)x.GetValue(field.Value), (float)y.GetValue(field.Value),
                    (float)z.GetValue(field.Value)
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

                using var temp = TempArray.Create([
                    (float)x.GetValue(configVal), (float)y.GetValue(configVal), (float)z.GetValue(configVal)
                ]);

                widget.Value = temp.AsReadOnlySpan();
            };
        }
        else if (field.Type == typeof(CommandColor) || field.Type == typeof(Color)) return colorField(field);
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
                    var value = Convert.ChangeType(widget.Value.ToString(), field.Type, CultureInfo.InvariantCulture);

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

        using var text = StringHelper.Interpolate($"Values of type {field.Type.Name} cannot be edited");
        return new Label(Manager)
        {
            StyleName = "listItem",
            Text = field.Value.ToString(),
            Tooltip = text.AsReadOnlySpan(),
            AnchorFrom = BoxAlignment.Right,
            AnchorTo = BoxAlignment.Right,
            CanGrow = false
        };
    }

    Vector2Picker vector2Field(EffectConfig.ConfigField field)
    {
        if (field.Type == typeof(Vector2))
        {
            Vector2Picker widget = new(Manager)
            {
                Value = (Vector2)field.Value,
                AnchorFrom = BoxAlignment.Right,
                AnchorTo = BoxAlignment.Right,
                CanGrow = false
            };

            widget.OnValueCommited += (_, _) =>
            {
                setFieldValue(field, (Vector2)widget.Value);

                widget.Value =
                    Unsafe.As<Vector2, CommandPosition>(ref Unsafe.Unbox<Vector2>(effect.Config.GetValue(field.Name)));
            };

            return widget;
        }

        {
            Vector2Picker widget = new(Manager)
            {
                Value = field.Type == typeof(CommandPosition) ?
                    Unsafe.Unbox<CommandPosition>(field.Value) :
                    Unsafe.As<CommandScale, CommandPosition>(ref Unsafe.Unbox<CommandScale>(field.Value)),
                AnchorFrom = BoxAlignment.Right,
                AnchorTo = BoxAlignment.Right,
                CanGrow = false
            };

            widget.OnValueCommited += (_, _) =>
            {
                if (field.Type == typeof(CommandPosition)) setFieldValue(field, widget.Value);
                else setFieldValue(field, (CommandScale)widget.Value);

                widget.Value = field.Type == typeof(Vector2) ?
                    Unsafe.Unbox<Vector2>(effect.Config.GetValue(field.Name)) :
                    Unsafe.As<CommandScale, CommandPosition>(
                        ref Unsafe.Unbox<CommandScale>(effect.Config.GetValue(field.Name)));
            };

            return widget;
        }
    }

    HsbColorPicker colorField(EffectConfig.ConfigField field)
    {
        if (field.Type == typeof(Color))
        {
            HsbColorPicker widget = new(Manager)
            {
                Value = Unsafe.Unbox<Color>(field.Value),
                AnchorFrom = BoxAlignment.Right,
                AnchorTo = BoxAlignment.Right,
                CanGrow = false
            };

            widget.OnValueCommited += (_, _) =>
            {
                setFieldValue(field, widget.Value);

                widget.Value = Unsafe.Unbox<Color>(effect.Config.GetValue(field.Name));
            };

            return widget;
        }

        {
            HsbColorPicker widget = new(Manager)
            {
                Value = Unsafe.Unbox<CommandColor>(field.Value),
                AnchorFrom = BoxAlignment.Right,
                AnchorTo = BoxAlignment.Right,
                CanGrow = false
            };

            widget.OnValueCommited += (_, _) =>
            {
                setFieldValue(field, (CommandColor)widget.Value);

                widget.Value = Unsafe.Unbox<CommandColor>(effect.Config.GetValue(field.Name));
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
        PoolingMemoryStream memory = new();
        try
        {
            using BinaryWriter writer = new(memory, Encoding.UTF8, true);

            writer.Write(effect.Config.Fields.Count);
            foreach (var field in effect.Config.Fields)
            {
                writer.Write(field.Name);
                ObjectSerializer.Write(writer, field.Value);
            }
        }
        catch (Exception e)
        {
            memory.Dispose();
            SDL.LogWarn(LogCategory.Application, $"Cannot copy clipboard data: {e}");
        }

        (ClipboardHelper.GetData() as IDisposable)?.Dispose();
        ClipboardHelper.SetData(memory);
    }

    void pasteConfiguration()
    {
        var changed = false;
        try
        {
            using BinaryReader reader = new((Stream)ClipboardHelper.GetData(), Encoding.UTF8, true);
            reader.BaseStream.Position = 0;

            var fieldCount = reader.ReadInt32();
            for (var i = 0; i < fieldCount; ++i)
            {
                var name = reader.ReadString();
                var value = ObjectSerializer.Read(reader);
                try
                {
                    foreach (var f in effect.Config.Fields)
                        if (f.Name == name)
                        {
                            if (f.Value.Equals(value)) break;

                            changed |= effect.Config.SetValue(name, value);
                            break;
                        }
                }
                catch (Exception ex)
                {
                    SDL.LogError(LogCategory.Application, $"Paste '{name}': {ex}");
                }
            }
        }
        catch (Exception ex)
        {
            SDL.LogWarn(LogCategory.Application, $"Cannot paste clipboard data: {ex}");
        }

        if (!changed) return;

        updateFields();
        effect.Refresh();
    }
}